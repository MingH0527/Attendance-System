using WebAndMobile.ViewModels;

namespace WebAndMobile.Services;

public interface ITimetableValidationService
{
    Task<ValidationResult> ValidateAsync(
        TimetableRequest request);
}