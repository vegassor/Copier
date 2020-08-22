namespace Copier
{
    public enum LoggingLevel
    {
        //don't log
        None = 0,
        //log fatal errors (program cannot continue its work)
        Fatal = 5,
        //log all errors
        Error = 10,
        //log errors and info e.g. copied directories
        Info = 15,
        //log everything (errors, info, debug info)
        Debug = 20,
    }
}
