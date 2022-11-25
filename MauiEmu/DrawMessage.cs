using CommunityToolkit.Mvvm.Messaging.Messages;

namespace MauiEmu;

public class DrawMessage : ValueChangedMessage<string>
{
    public DrawMessage(string value) : base(value)
    {
    }
}
