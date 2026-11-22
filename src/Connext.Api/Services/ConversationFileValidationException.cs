namespace Connext.Api.Services;

public class ConversationFileValidationException : InvalidOperationException
{
    public ConversationFileValidationException(string message) : base(message)
    {
    }
}
