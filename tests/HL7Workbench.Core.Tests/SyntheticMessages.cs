namespace HL7Workbench.Core.Tests;

internal static class SyntheticMessages
{
    internal const string Header = "MSH|^~\\&|SYNTHETIC|LAB|WORKBENCH|TEST|20261013120000||ADT^A01|SYN-0001|T|2.5";
    internal const string Patient = "PID|1||SYN-001^^^TEST^MR~ALT-001^^^TEST^PI||Example^Ada^Q";
    internal static string Message(string ending = "\r") => Header + ending + Patient + ending;
}
