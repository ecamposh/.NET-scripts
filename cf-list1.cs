using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace RackspaceCloudFilesList
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: RackspaceListContainers.exe <username> <apiKey> <region>");
                Console.WriteLine("Example: RackspaceListContainers.exe myuser 0123456789abcdef LON");
                return;
            }

            string username = args[0];
            string apiKey = args[1];
            string region = args[2].ToUpper();

            string identityEndpoint = "https://identity.api.rackspacecloud.com/v2.0";
            if (region == "LON")
            {
                identityEndpoint = "https://lon.identity.api.rackspacecloud.com/v2.0";
            }
            // Note: If other regions need different identity, adjust accordingly. For now, assuming US or LON.

            try
            {
                // Step 1: Authenticate
                string authUrl = identityEndpoint + "/tokens";
                string authBody = "{\"auth\":{\"RAX-KSKEY:apiKeyCredentials\":{\"username\":\"" + username + "\",\"apiKey\":\"" + apiKey + "\"}}}";

                HttpWebRequest authRequest = (HttpWebRequest)WebRequest.Create(authUrl);
                authRequest.Method = "POST";
                authRequest.ContentType = "application/json";

                using (StreamWriter writer = new StreamWriter(authRequest.GetRequestStream()))
                {
                    writer.Write(authBody);
                }

                string authResponseJson;
                using (HttpWebResponse response = (HttpWebResponse)authRequest.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    authResponseJson = reader.ReadToEnd();
                }

                JavaScriptSerializer serializer = new JavaScriptSerializer();
                dynamic authResponse = serializer.Deserialize<dynamic>(authResponseJson);

                string token = authResponse["access"]["token"]["id"];

                // Find the storage URL for cloudFiles in the specified region
                string storageUrl = null;
                dynamic serviceCatalog = authResponse["access"]["serviceCatalog"];
                foreach (dynamic service in serviceCatalog)
                {
                    if (service["name"] == "cloudFiles")
                    {
                        dynamic endpoints = service["endpoints"];
                        foreach (dynamic endpoint in endpoints)
                        {
                            if (endpoint["region"] == region)
                            {
                                storageUrl = endpoint["publicURL"];
                                break;
                            }
                        }
                        if (storageUrl != null) break;
                    }
                }

                if (storageUrl == null)
                {
                    Console.WriteLine("Could not find cloudFiles endpoint for region: " + region);
                    return;
                }

                // Step 2: List containers
                string listUrl = storageUrl + "?format=json";

                HttpWebRequest listRequest = (HttpWebRequest)WebRequest.Create(listUrl);
                listRequest.Method = "GET";
                listRequest.Headers.Add("X-Auth-Token", token);
                listRequest.Accept = "application/json";

                string listResponseJson;
                using (HttpWebResponse response = (HttpWebResponse)listRequest.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    listResponseJson = reader.ReadToEnd();
                }

                dynamic containers = serializer.Deserialize<dynamic>(listResponseJson);

                Console.WriteLine("Containers in region " + region + ":");
                foreach (dynamic container in containers)
                {
                    Console.WriteLine("- " + container["name"] + " (Files: " + container["count"] + ", Bytes: " + container["bytes"] + ")");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }
    }
}
