using Blazorise;
using MagiCommon;

namespace MagiCloud;

public class ExtendedValidationRules
{
    public static void IsURI(ValidatorEventArgs e)
    {
        var urlString = e.Value as string;
        var valid = Validators.IsValidURI(urlString);
        if (valid is null)
        {
            e.Status = ValidationStatus.None;
        }
        else
        {
            e.Status = valid.Value ? ValidationStatus.Success : ValidationStatus.Error;
        }
    }

    public static void IsFileName(ValidatorEventArgs e)
    {
        var fileName = e.Value as string;
        var valid = Validators.IsValidFileName(fileName);
        if (valid is null)
        {
            e.Status = ValidationStatus.None;
        }
        else
        {
            e.Status = valid.Value ? ValidationStatus.Success : ValidationStatus.Error;
        }
    }

    public static void IsFilePath(ValidatorEventArgs e)
    {
        var filePath = e.Value as string;
        var valid = Validators.IsValidFilePath(filePath);
        if (valid is null)
        {
            e.Status = ValidationStatus.None;
        }
        else
        {
            e.Status = valid.Value ? ValidationStatus.Success : ValidationStatus.Error;
        }
    }
}
