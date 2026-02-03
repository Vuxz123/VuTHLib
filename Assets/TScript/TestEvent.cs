using _VuTH.Common.MessagePipe.Attributes;
[assembly: VuTHMessagePipeEventAssembly]
namespace TScript
{
    [MessagePipeEvent(EventScope.Scene, "Gameplay")]
    public class TestEvent
    {
        public string Message { get; }

        public TestEvent(string message)
        {
            Message = message;
        }
    }
    
    
    [MessagePipeEvent(EventScope.Scene, "Gameplay2")]
    public class TestEvent2
    {
        public string Message { get; }

        public TestEvent2(string message)
        {
            Message = message;
        }
    }
    
    [MessagePipeEvent(EventScope.Scene, "Gameplay3")]
    public class TestEvent3
    {
        public string Message { get; }

        public TestEvent3(string message)
        {
            Message = message;
        }
    }
}