using ThreeDPrintStore.Data;
using ThreeDPrintStore.Models;

namespace ThreeDPrintStore.Services;

public sealed class PrintRequestSubmission
{
    private readonly ApplicationDbContext _db;
    private readonly PrivateUploadStore _uploads;

    public PrintRequestSubmission(ApplicationDbContext db, IWebHostEnvironment environment)
    {
        _db = db;
        _uploads = new PrivateUploadStore(environment);
    }

    public async Task<PrintRequest> SaveAsync(PrintRequestInput input, CancellationToken cancellationToken)
    {
        string? model = null;
        string? image = null;
        try
        {
            model = await _uploads.SaveAsync(input.ModelFile!, false, cancellationToken);
            if (input.ReferenceImage != null)
                image = await _uploads.SaveAsync(input.ReferenceImage, true, cancellationToken);
            var request = new PrintRequest
            {
                CustomerName = input.CustomerName.Trim(),
                CustomerEmail = input.CustomerEmail.Trim(),
                ProjectDescription = input.ProjectDescription.Trim(),
                RequestedColorsCount = input.RequestedColorsCount,
                MaterialType = "PLA",
                FilePath = model,
                ReferenceImagePath = image,
                Status = QuoteStatus.PendingReview,
                SubmittedAt = DateTime.UtcNow
            };
            _db.PrintRequests.Add(request);
            await _db.SaveChangesAsync(cancellationToken);
            return request;
        }
        catch
        {
            _uploads.Delete(model);
            _uploads.Delete(image);
            throw;
        }
    }
}
