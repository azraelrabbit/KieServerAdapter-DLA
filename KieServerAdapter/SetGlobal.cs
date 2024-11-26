using Newtonsoft.Json;

namespace KieServerAdapter
{
    public class SetGlobal : ICommandContainer
    {
        [JsonProperty("set-global")]
        public ICommand Command { get; }

        public SetGlobal(string identifier, object commandObject, string objectNameSpace)
        {
            Command = new CommandSetGlobal { Identifier = identifier, CommandObject = new CommandObject(commandObject, objectNameSpace) };
        }
    }

    public class SetGlobalBase : ICommandContainer
    {
        [JsonProperty("set-global")]
        public ICommand Command { get; }

        public SetGlobalBase(string identifier, object commandObject)
        {
            Command = new CommandSetGlobalBase
            {
                Identifier = identifier,
                CommandObject = commandObject,
                OutIdentifier = identifier
            };
        }
    }
}
