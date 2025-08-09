using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace RackspaceCloudFiles
{
    class Program
    {
        private static readonly string Username = "yourUserName";
        private static readonly string ApiKey = "yourApiKey";
        private static readonly string Region = "LON";

        static async Task Main(string[] args)
        {
            try
            {
                System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
                string identityEndpoint = Region.ToUpper() == "LON" 
                    ? "https://lon.identity.api.rackspacecloud.com/v2.0" 
                    : "https://identity.api.rackspacecloud.com/v2.0";

                string token = await AuthenticateAsync(identityEndpoint);
                if (string.IsNullOrEmpty(token))
                {
                    Console.WriteLine("Authentication failed.");
                    return;
                }

                string cloudFilesEndpoint = await GetCloudFilesEndpointAsync(identityEndpoint, token, Region);
                if (string.IsNullOrEmpty(cloudFilesEndpoint))
                {
                    Console.WriteLine("No Cloud Files endpoint found for region " + Region + ".");
                    return;
                }

                await ListContainersAsync(cloudFilesEndpoint, token);
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
            }
        }

        static async Task<string> AuthenticateAsync(string identityEndpoint)
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

                var response = await client.PostAsync(identityEndpoint + "/tokens", content);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine("Authentication request failed: " + response.StatusCode);
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(responseContent);
                return json["access"]?["token"]?["id"]?.ToString();
            }
        }

        static async Task<string> GetCloudFilesEndpointAsync(string identityEndpoint, string token, string region)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("X-Auth-Token", token);
                var response = await client.GetAsync(identityEndpoint + "/tokens");
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine("Failed to retrieve service catalog: " + response.StatusCode);
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(responseContent);
                var serviceCatalog = json["access"]?["serviceCatalog"];
                foreach (var service in serviceCatalog)
                {
                    if (service["type"]?.ToString() == "object-store")
                    {
                        foreach (var endpoint in service["endpoints"])
                        {
                            if (endpoint["region"]?.ToString().ToUpper() == region.ToUpper())
                            {
                                return endpoint["publicURL"]?.ToString();
                            }
                        }
                    }
                }
                return null;
            }
        }

        static async Task ListContainersAsync(string cloudFilesEndpoint, string token)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("X-Auth-Token", token);
                var response = await client.GetAsync(cloudFilesEndpoint);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine("Failed to list containers: " + response.StatusCode);
                    return;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var containers = JArray.Parse(responseContent);
                Console.WriteLine("Containers in region " + Region + ":");
                foreach (var container in containers)
                {
                    Console.WriteLine("- " + container["name"] + " (Objects: " + container["count"] + ", Bytes: " + container["bytes"] + ")");
                }
            }
        }
    }
}
