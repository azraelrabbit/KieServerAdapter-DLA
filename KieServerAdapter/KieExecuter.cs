using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using RestSharp.Authenticators;
using RestSharp.Serializers.NewtonsoftJson;

namespace KieServerAdapter
{
    public class KieExecuter:IDisposable
    {
        protected const string ApplicationType = "application/json";
        protected const string AutenticationType = "Basic";
        protected const string DefaultInstancesPath = "/services/rest/server/containers/instances/";

        [JsonIgnore]
        private readonly bool perRequestAuth;

        [JsonIgnore]
        private RestClient _restClient;
        [JsonIgnore]
        private HttpBasicAuthenticator _authenticator;

        [JsonIgnore]
        public string HostUrl { get; set; }

        [JsonIgnore]
        public string InstancesPath { get; set; } = "kie-server";

        [JsonIgnore]
        public string AuthUserName { get; set; }

        [JsonIgnore]
        public string AuthPassword { get; set; }

        [JsonProperty("lookup")]
        public string LookUp { get; set; } = "defaultKieSession";


        public KieExecuter(string hostUrl,string authUserName,string authPassword,bool perRequestAuth=false) { 
            
            HostUrl = hostUrl;
            AuthUserName = authUserName;
            AuthPassword = authPassword;
            this.perRequestAuth = perRequestAuth;
            _authenticator =new HttpBasicAuthenticator(AuthUserName, AuthPassword);

            RestClientOptions options= new RestClientOptions(HostUrl);

            options.Authenticator= _authenticator;
            
 
            _restClient = new RestClient(options, configureSerialization: s => s.UseNewtonsoftJson());

            
        }

        /// <summary>
        /// Re-sort the commands in the batch by priority as listed in KieCommandTypeEnum.
        /// Defaults to true for backwards compatibility with the original library, but
        /// that behavior may not be what you want if you need a specific order to 
        /// your commands, such as interspersing GetObjects or Query command before and
        /// after FireAllRules.
        /// </summary>
        [JsonIgnore]
        public bool SortCommandBatch { get; set; } = true;

        [JsonProperty("commands")]
        public List<ICommandContainer> Commands { get; private set; } = new List<ICommandContainer>();

        public void StartProcess(string processId)
        {
            Commands.Add(new StartProcess(processId));
        }

        public void Insert(object commandObject, string objectNameSpace, bool returnObject = true)
        {
            Commands.Add(new Insert(commandObject, objectNameSpace, returnObject));
        }

        public void Insert(object commandObject, string objectNameSpace, string outIdentifier, bool returnObject = true)
        {
            Commands.Add(new Insert(commandObject, objectNameSpace, outIdentifier, returnObject));
        }

        /// <summary>
        /// insert an object whose type name will come from a DroolsType attribute on the class 
        /// of the object being inserted
        /// </summary>
        public void InsertType(object commandObject, bool returnObject = true)
        {
            var objectNameSpace = commandObject.GetType().GetAttributeValue<DroolsTypeAttribute, string>(a => a.TypeName);
            Insert(commandObject, objectNameSpace, returnObject);
        }

        /// <summary>
        /// insert an object whose type name will come from a DroolsType attribute on the class 
        /// of the object being inserted with an explicit out-identifier
        /// </summary>
        public void InsertType(object commandObject, string outIdentifier, bool returnObject = true)
        {
            var objectNameSpace = commandObject.GetType().GetAttributeValue<DroolsTypeAttribute, string>(a => a.TypeName);
            Insert(commandObject, objectNameSpace, outIdentifier, returnObject);
        }


        public void SetGlobal(string identifier, object commandObject, string objectNameSpace)
        {
            Commands.Add(new SetGlobal(identifier, commandObject, objectNameSpace));
        }

        public void SetGlobal(string identifier, object commandObject)
        {
            var objectNameSpace = commandObject.GetType().GetAttributeValue<DroolsTypeAttribute, string>(a => a.TypeName);
            SetGlobal(identifier, commandObject, objectNameSpace);
        }

        public void SetGlobalBase(string identifier, object commandObject)
        {
            Commands.Add(new SetGlobalBase(identifier, commandObject));
        }

        public void GetGlobal(string identifier)
        {
            Commands.Add(new GetGlobal(identifier,identifier));
        }

        public void GetObjects(string outIdentifier = null)
        {
            Commands.Add(new GetObjects(outIdentifier));
        }

        public void FireAllRules()
        {
            FireAllRules(-1);
        }

        public void FireAllRules(int max)
        {
            Commands.Add(new FireAllRules(max));
        }

        public void FireAllRulesEmpty()
        {
            Commands.Add(new FireAllRulesEmpty());
        }


        public void Query(string identifier, string outIdentifier = null, IEnumerable<object> arguments = null)
        {
            Commands.Add(new Query(identifier, outIdentifier, arguments));
        }

        public async Task<ExecutionResponse<object>> ExecuteAsync(string containerName)
        {
            return await ExecuteAsync<object>(containerName);
        }

        public async Task<ExecutionResponse<T>> ExecuteAsync<T>(string containerName)
        {
            return await ExecuteAsync<T>(containerName, KieCommandTypeEnum.Insert);
        }

        public async Task<ExecutionResponse<T>> ExecuteGlobalAsync<T>(string containerName)
        {
            return await ExecuteAsync<T>(containerName, KieCommandTypeEnum.SetGlobal);
        }

        public async Task<ExecutionResponse<T>> ExecuteAsync<T>(string containerName, KieCommandTypeEnum commandType)
        {
            var startDate = DateTime.Now;

            ExecutionResponse<T> result = await ExecuteCall<T>(containerName);

            ProcessResult(result, commandType);

            var span = DateTime.Now - startDate;

            result.ElapsedTime = (int)span.TotalMilliseconds;

            return result;
        }

        private async Task<ExecutionResponse<T>> ExecuteCall<T>(string containerName)
        {
            if (SortCommandBatch)
            {
                Commands = Commands.OrderByDescending(c => c.Command.CommandType).ToList();
            }
                
            var json = JsonConvert.SerializeObject(this);
            var result = new ExecutionResponse<T>();


            var executeUrl = string.Concat(InstancesPath, DefaultInstancesPath, containerName);


           //var response= await _restClient.PostJsonAsync(executeUrl, json);

           // _restClient.po

            var request=new RestRequest(executeUrl,Method.Post);

            //request.AddHeader("Content-Type","application/json");

            //add accept header for json
            request.AddHeader("Accept","application/json");

            if (perRequestAuth)
            {
                request.Authenticator=_authenticator;
            }

            request.AddJsonBody(json);

            var response=await _restClient.ExecuteAsync<ExecutionResponse<T>>(request);

             
            if (response.IsSuccessStatusCode)
            {
                result = response.Data;                
            }

            if (result != null)
            {
                result.ResponseBody = response.Content;
                result.RequestBody = json;
            }

             
            //using (var client = new HttpClient { BaseAddress = new Uri(HostUrl) })
            //{

            //    client.DefaultRequestHeaders.Clear();
            //    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(ApplicationType));

            //    if (!string.IsNullOrEmpty(AuthUserName))
            //    {
            //        var byteArray = Encoding.ASCII.GetBytes($"{AuthUserName}:{AuthPassword}");
            //        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(AutenticationType, Convert.ToBase64String(byteArray));
            //    }

            //    using (var request = new HttpRequestMessage(HttpMethod.Post, string.Concat(InstancesPath, DefaultInstancesPath, containerName)))
            //    {
            //        request.Content = new StringContent(json);
            //        request.Content.Headers.ContentType = new MediaTypeHeaderValue(ApplicationType);

            //        using (var response = await client.SendAsync(request))
            //        {
            //            if (response.IsSuccessStatusCode)
            //            {
            //                result = await response.Content.ReadAsAsync<ExecutionResponse<T>>();
            //                result.ResponseBody = await response.Content.ReadAsStringAsync();
            //            }
            //        }
            //    }
            //}

            //result.RequestBody = json;

            return result;
        }

        private void ProcessResult<T>(ExecutionResponse<T> result, KieCommandTypeEnum commandType)
        {
            if (typeof(T) != typeof(object))
            {
                try
                {
                    var command = Commands.FirstOrDefault(c => c.Command.CommandType == commandType);
                    var identifier = command?.Command as ICommandOutIdentifier;
                    var outObject = result.Result?.ExecutionResults.Results.SingleOrDefault(e => e.Key == identifier?.OutIdentifier);

                    if (outObject != null)
                    {
                        var item = (JObject)outObject.Value.Value;

                        if (item != null)
                        {
                            var first = item.First;

                            if (first is JProperty)
                            {
                                var prop = first as JProperty;
                                var commandObject = command?.Command as ICommandObject;
                                var v = prop.Name.Equals(commandObject?.CommandObject);
                                if (true)
                                {
                                    result.SmartSingleResponse = JsonConvert.DeserializeObject<T>(prop.Value.ToString(), new UnixTimestampConverter());
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }

        public void Dispose()
        {
            _restClient?.Dispose();
        }
    }

}
