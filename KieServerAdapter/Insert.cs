using Newtonsoft.Json;

namespace KieServerAdapter
{
    public class Insert : ICommandContainer
    {
        [JsonProperty("insert")]
        public ICommand Command { get; }

        public Insert(object commandObject, string objectNameSpace, bool returnObject = true)
        {
            Command = new CommandInsert { 
                CommandObject = new CommandObject(commandObject, objectNameSpace),
                ReturnObject = returnObject };
        }
    }
}
