﻿using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.AspNet.SignalR;
using Spark.Core;
using Spark.Engine.Core;
using Spark.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;


namespace Spark.Import
{
    public class ImportProgressMessage
    {
        public int Progress;
        public string Message;
    }

    public class InitializeHub : Hub
    {
        private List<Resource> resources;

        private FhirService fhirService;
        private ILocalhost localhost;
        private IFhirStore fhirStore;
        private IFhirIndex fhirIndex;

        private int ResourceCount;

        public InitializeHub(FhirService fhirService, ILocalhost localhost, IFhirStore fhirStore, IFhirIndex fhirIndex)
        {
            this.localhost = localhost;
            this.fhirService = fhirService;
            this.fhirStore = fhirStore;
            this.fhirIndex = fhirIndex;
            this.resources = null;
        }

        public List<Resource> GetExampleData()
        {
            var list = new List<Resource>();

            Bundle data = Examples.ImportEmbeddedZip().LimitPerType(50).ToBundle(localhost.DefaultBase); 

            if (data.Entry != null && data.Entry.Count() != 0)
            {
                foreach (var entry in data.Entry)
                {
                    if (entry.Resource != null)
                    {
                        list.Add((Resource)entry.Resource);
                    }
                }
            }
            return list;
        }

        private int _progress = 0;

        private void Progress(string message, int progress)
        {

            _progress = progress;

            var msg = new ImportProgressMessage
            {
                Message = message,
                Progress = progress
            };

            Clients.Caller.sendMessage(msg);
        }

        private void Progress(string message)
        {
            Progress(message, _progress);
        }

        private ImportProgressMessage Message(string message, int idx)
        {
            var msg = new ImportProgressMessage
            {
                Message = message,
                Progress = (int)10 + (idx + 1) * 90 / ResourceCount
            };
            return msg;
        }
        public void LoadData()
        {
            var messages = new StringBuilder();
            messages.AppendLine("Import completed!");
            try
            {
                //cleans store and index
                Progress("Clearing the database...", 0);
                fhirStore.Clean();
                fhirIndex.Clean();

                Progress("Loading examples data...", 5);
                this.resources = GetExampleData();

                var resarray = resources.ToArray();
                ResourceCount = resarray.Count();

                for (int x = 0; x <= ResourceCount - 1; x++)
                {
                    var res = resarray[x];
                    // Sending message:
                    var msg = Message("Importing " + res.ResourceType.ToString() + " " + res.Id + "...", x);
                    Clients.Caller.sendMessage(msg);

                    try
                    {
                        //Thread.Sleep(1000);
                        Key key = res.ExtractKey();

                        if (res.Id != null && res.Id != "")
                        {

                            fhirService.Put(key, res);
                        }
                        else
                        {
                            fhirService.Create(key, res);
                        }
                    }
                    catch (Exception e)
                    {
                        // Sending message:
                        var msgError = Message("ERROR Importing " + res.ResourceType.ToString() + " " + res.Id + "... ", x);
                        Clients.Caller.sendMessage(msg);
                        messages.AppendLine(msgError.Message + ": " + e.Message);
                    }


                }

                Progress(messages.ToString(), 100);
            }
            catch (Exception e)
            {
                Progress("Error: " + e.Message);
            }
        }
    }

}
