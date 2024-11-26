using Newtonsoft.Json;

namespace KieServerAdapter
{
    public class GetGlobal : ICommandContainer
    {
        [JsonProperty("get-global")]
        public ICommand Command { get; }

        public GetGlobal(string identifier)
        {
            Command = new CommandGetGlobal { Identifier = identifier };
        }

        public GetGlobal(string identifier,string outIdentifier)
        {
            Command = new CommandGetGlobal { Identifier = identifier,OutIdentifier=outIdentifier };
        }
    }
}
