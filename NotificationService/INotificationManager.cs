using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NotificationService
{
    public interface INotificationManager
    {
        Task Success(string message);

        Task Error(string message);

        Task Warning(string message);

        Task Information(string message);

        Task HubStatus(string message);

        Task TemporaryPassword(string email, string temporaryPassword);
    }
}
