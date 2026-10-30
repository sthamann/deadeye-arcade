using System.ComponentModel;

namespace Reaper.Core;

public record GameTestFailure(string Category,string Message,string ExceptionType,int HResult,int? NativeCode)
{
    public static GameTestFailure From(Exception error)
    {
        Exception cause=error;
        for(Exception? candidate=error;candidate is not null;candidate=candidate.InnerException)
        {
            cause=candidate;
            if(candidate is Win32Exception || ((uint)candidate.HResult & 0xffff0000)==0x80070000)break;
        }
        int? native=cause is Win32Exception win32 ? win32.NativeErrorCode : ((uint)cause.HResult & 0xffff0000)==0x80070000 ? cause.HResult & 0xffff : null;
        string category=native switch {
            4551=>"windows-app-control",577=>"signature-validation",126 or 14001=>"missing-runtime",
            193=>"architecture-mismatch",740=>"elevation-required",2 or 3=>"missing-file",_=>"launch-error"
        };
        return new(category,cause.Message,cause.GetType().FullName??cause.GetType().Name,cause.HResult,native);
    }
}
