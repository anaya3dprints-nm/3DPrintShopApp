//This means the ErrorViewModel class belongs to the Models folder in my project
namespace ThreeDPrintStore.Models;

public class ErrorViewModel //this class is used by ASP.NET Core's built-in error page
{
    public string? RequestId { get; set; } //defines a nullable string property called RequesId


    /**
        Defines a read‑only boolean property called ShowRequestId.
        The arrow syntax (=>) means this is a computed property, not a stored value.
        What does it do?
            It checks whether RequestId is not null and not an empty string.
            If the request ID exists, the property returns true.
            If there is no request ID, it returns false.    
The MVC error view uses this property to decide whether it should display the request ID to the user.
    **/
        
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
