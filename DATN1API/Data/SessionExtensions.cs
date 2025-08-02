using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
//Newtonsoft.Json trong NuGet
namespace DATN1API.Extensions
{
    public static class SessionExtensions
    {
        // Phương thức để lưu object vào Session
        public static void SetObject(this ISession session, string key, object value)
        {
            if (session.IsAvailable)
            {
                session.SetString(key, JsonConvert.SerializeObject(value));
            }
            else
            {
                // Log or handle the situation when session is unavailable
                throw new InvalidOperationException("Session is not available.");
            }
        }

        public static T GetObject<T>(this ISession session, string key)
        {
            if (!session.IsAvailable)
            {
                // Log or handle the situation when session is unavailable
                throw new InvalidOperationException("Session is not available.");
            }

            var value = session.GetString(key);
            return value == null ? default(T) : JsonConvert.DeserializeObject<T>(value);
        }

    }
}
