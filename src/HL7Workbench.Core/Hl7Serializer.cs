namespace HL7Workbench.Core;

public static class Hl7Serializer
{
    public static byte[] Serialize(Hl7Message message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return message.SerializeBytes();
    }

    public static string SerializeText(Hl7Message message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.Diagnostics.Any(d => d.Code == "InvalidEncoding"))
            throw new InvalidOperationException("Undecodable input is available only as original bytes.");
        return message.RawText;
    }
}
