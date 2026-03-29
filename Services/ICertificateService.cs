using DACSWEBSK.Models;

namespace DACSWEBSK.Services
{
    public interface ICertificateService
    {
        Task<Certificate> GenerateCertificateAsync(Attendee attendee, Event @event);
        Task<bool> SendCertificateEmailAsync(Certificate certificate);
        Task<IEnumerable<Certificate>> GetCertificatesByEventAsync(int eventId);
        Task<IEnumerable<Certificate>> GetCertificatesByAttendeeEmailAsync(string email);
        Task<bool> VerifyCertificateAsync(string certificateNumber);
    }
} 