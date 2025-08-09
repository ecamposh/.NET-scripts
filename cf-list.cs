// Compile with C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /r:Newtonsoft.Json.dll /r:System.Net.Http.dll /r:System.Runtime.Serialization.dll ListRackspaceContainers.cs
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Net;

namespace RackspaceCloudFiles
{
    class Program
    {
        // Configuration - Replace with your credentials and desired region
        private static readonly string Username = "yourUserName"; // Replace with your Rackspace username
        private static readonly string ApiKey = "yourApiKey"; // Replace with your Rackspace API key
        private static readonly string Region = "LON"; // Region to list containers (e.g., LON, DFW, ORD)
        private static readonly string IdentityEndpoint = Region == "LON" 
            ? "https://lon.identity.api.rackspacecloud.com/v2.0/tokens" 
            : "https://identity.api.rackspacecloud.com/v2.0/tokens";

        static async Task Main(string[] args)
        {
            // Ensure TLSv1.2 is used
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            try
            {
                // Step 1: Authenticate and get token
                string authToken = await GetAuthTokenAsync();
                if (string.IsNullOrEmpty(authToken))
                {
                    Console.WriteLine("Authentication failed.");
                    return;
                }

                // Step 2: Get Cloud Files endpoint for the specified region
                string cloudFilesEndpoint = await GetCloudFilesEndpointAsync(authToken);
                if (string.IsNullOrEmpty(cloudFilesEndpoint))
                {
                    Console.WriteLine($"No Cloud Files endpoint found for region {Region}.");
                    return;
                }

                // Step 3: List containers
                await ListContainersAsync(authToken, cloudFilesEndpoint);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }

        static async Task<string> GetAuthTokenAsync()
        {
            using (var client = new HttpClient())
            {
                // Prepare authentication request payload
                var authRequest = new
                {
                    auth = new
                    {
                        RAX_KSKEY_apiKeyCredentials = new
                        {
                            username = Username,
                            apiKey = ApiKey
                        }
                    }
                };

                var content = new StringContent(
                    Newtonsoft.Json.JsonConvert.SerializeObject(authRequest),
                    Encoding.UTF8,
                    "application/json");

                // Send authentication request
                var response = await client.PostAsync(IdentityEndpoint, content);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Authentication request failed: {response.StatusCode}");
                    return null;
                }

                // Parse response to extract token
                var responseContent = await response.Content.ReadAsStringAsync();
                var jsonResponse = JObject.Parse(responseContent);
                string token = jsonResponse["access"]?["token"]?["id"]?.ToString();

                return token;
            }
        }

        static async Task<string> GetCloudFilesEndpointAsync(string authToken)
        {
            using (var client = new HttpClient())
            {
                // Set authentication header
                client.DefaultRequestHeaders.Add("X-Auth-Token", authToken);

                // Request service catalog
                var response = await client.PostAsync(IdentityEndpoint, null);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Failed to retrieve service catalog: {response.StatusCode}");
                    return null;
                }

                // Parse service catalog to find Cloud Files endpoint for the specified region
                var responseContent = await response.Content.ReadAsStringAsync();
                var jsonResponse = JObject.Parse(responseContent);
                var serviceCatalog = jsonResponse["access"]?["serviceCatalog"]?.Children();

                foreach (var service in serviceCatalog)
                {
                    if (service["name"]?.ToString() == "cloudFiles")
                    {
                        foreach (var endpoint in service["endpoints"])
                        {
                            if (endpoint["region"]?.ToString() == Region)
                            {
                                return endpoint["publicURL"]?.ToString();
                            }
                        }
                    }
                }

                return null;
            }
        }

        static async Task ListContainersAsync(string authToken, string cloudFilesEndpoint)
        {
            using (var client = new HttpClient())
            {
                // Set authentication header
                client.DefaultRequestHeaders.Add("X-Auth-Token", authToken);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // Send request to list containers
                var response = await client.GetAsync(cloudFilesEndpoint);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Failed to list containers: {response.StatusCode}");
                    return;
                }

                // Parse and display containers
                var responseContent = await response.Content.ReadAsStringAsync();
                var containers = JArray.Parse(responseContent);

                Console.WriteLine($"Containers in region {Region}:");
                foreach (var container in containers)
                {
                    Console.WriteLine($"- {container["name"]}");
                }
            }
        }
    }
}
