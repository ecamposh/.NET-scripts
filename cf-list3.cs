using System;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json.Linq;

namespace RackspaceCloudFiles
{
    class Program
    {
        private static readonly string Username = "yourUserName"; // Replace with your Rackspace username
        private static readonly string ApiKey = "yourApiKey";     // Replace with your Rackspace API key
        private static readonly string Region = "LON";            // Specify the region (e.g., LON, DFW, ORD)

        static void Main(string[] args)
        {
            try
            {
                // Ensure TLSv1.2 is used
                System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

                // Determine the identity endpoint based on region
                string identityEndpoint = Region.ToUpper() == "LON"
                    ? "https://lon.identity.api.rackspacecloud.com/v2.0"
                    : "https://identity.api.rackspacecloud.com/v2.0";

                // Step 1: Authenticate to get the token
                string token = Authenticate(identityEndpoint);
                if (string.IsNullOrEmpty(token))
                {
                    Console.WriteLine("Authentication failed.");
                    return;
                }

                // Step 2: Get the Cloud Files endpoint for the specified region
                string cloudFilesEndpoint = GetCloudFilesEndpoint(identityEndpoint, token, Region);
                if (string.IsNullOrEmpty(cloudFilesEndpoint))
                {
                    Console.WriteLine("No Cloud Files endpoint found for region " + Region + ".");
                    return;
                }

                // Step 3: List containers
                ListContainers(cloudFilesEndpoint, token);
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
            }
        }

        static string Authenticate(string identityEndpoint)
        {
            using (var client = new HttpClient())
            {
                var authRequest = new
                {
                    auth = new
                    {
                        RAXKSKEY_apiKeyCredentials = new
                        {
                            username = Username,
                            apiKey = ApiKey
                        }
                    }
                };

                var content = new StringContent(
                    JsonConvert.SerializeObject(authRequest),
                    Encoding.UTF8,
                    "application/json");

                var response = client.PostAsync(identityEndpoint + "/tokens", content).GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine("Authentication request failed: " + response.StatusCode);
                    return null;
                }

                var responseContent = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                var json = JObject.Parse(responseContent);
                var access = json["access"];
                if (access == null) return null;
                var token = access["token"];
                if (token == null) return null;
                var id = token["id"];
                return id != null ? id.ToString() : null;
            }
        }

        static string GetCloudFilesEndpoint(string identityEndpoint, string token, string region)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("X-Auth-Token", token);
                var response = client.GetAsync(identityEndpoint + "/tokens").GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine("Failed to retrieve service catalog: " + response.StatusCode);
                    return null;
                }

                var responseContent = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                var json = JObject.Parse(responseContent);
                var access = json["access"];
                if (access == null) return null;
                var serviceCatalog = access["serviceCatalog"];
                if (serviceCatalog == null) return null;

                foreach (var service in serviceCatalog)
                {
                    if (service["type"] != null && service["type"].ToString() == "object-store")
                    {
                        var endpoints = service["endpoints"];
                        if (endpoints != null)
                        {
                            foreach (var endpoint in endpoints)
                            {
                                if (endpoint["region"] != null && endpoint["region"].ToString().ToUpper() == region.ToUpper())
                                {
                                    var publicURL = endpoint["publicURL"];
                                    if (publicURL != null)
                                    {
                                        return publicURL.ToString();
                                    }
                                }
                            }
                        }
                    }
                }
                return null;
            }
        }

        static void ListContainers(string cloudFilesEndpoint, string token)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("X-Auth-Token", token);
                var response = client.GetAsync(cloudFilesEndpoint).GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine("Failed to list containers: " + response.StatusCode);
                    return;
                }

                var responseContent = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                var containers = JArray.Parse(responseContent);
                Console.WriteLine("Containers in region " + Region + ":");
                foreach (var container in containers)
                {
                    var name = container["name"];
                    var count = container["count"];
                    var bytes = container["bytes"];
                    Console.WriteLine("- " + name + " (Objects: " + count + ", Bytes: " + bytes + ")");
                }
            }
        }
    }
}
