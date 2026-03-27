namespace DalamudMCP.Framework;

public interface IResultFormatter<in TResult>
{
    public string? FormatText(TResult result, OperationContext context);
}


