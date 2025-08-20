using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace DATNAPI1
{
    public class VnPayLibrary
    {
        public const string VERSION = "2.1.0";

        private readonly SortedList<string, string> _requestData = new SortedList<string, string>(new VnPayCompare());
        private readonly SortedList<string, string> _responseData = new SortedList<string, string>(new VnPayCompare());

        // -----------------------------
        // Add request / response data
        // -----------------------------
        public void AddRequestData(string key, string value)
        {
            if (!string.IsNullOrEmpty(value))
                _requestData[key] = value;
        }

        public void AddResponseData(string key, string value)
        {
            if (!string.IsNullOrEmpty(value))
                _responseData[key] = value;
        }

        public string GetResponseData(string key)
            => _responseData.TryGetValue(key, out var ret) ? ret : string.Empty;

        // -----------------------------
        // Build request hashData
        // -----------------------------
        private static string UrlEncodeVNPay(string value)
        {
            // Encode tối giản & nhất quán (đừng thay %7E -> ~ hay v.v.)
            return WebUtility.UrlEncode(value);
        }

        private string BuildRequestHashData()
        {
            return string.Join("&",
                _requestData
                    .Where(kv => !string.IsNullOrEmpty(kv.Value))
                    .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                    .Select(kv => $"{kv.Key}={UrlEncodeVNPay(kv.Value)}"));
        }

        // -----------------------------
        // Create payment URL + expose hashData & HMAC for debug
        // -----------------------------
        public string CreateRequestUrlAndGetDebug(string baseUrl, string vnpHashSecret, out string hashData, out string vnpSecureHashUpper)
        {
            baseUrl = (baseUrl ?? string.Empty).Trim().TrimEnd('?');
            hashData = BuildRequestHashData();
            vnpSecureHashUpper = HmacSha512Public((vnpHashSecret ?? string.Empty).Trim(), hashData).ToUpperInvariant();
            return $"{baseUrl}?{hashData}&vnp_SecureHash={vnpSecureHashUpper}";
        }

        // (Giữ lại method cũ cho tương thích – nếu nơi khác đang gọi)
        public string CreateRequestUrlRFC3986(string baseUrl, string vnpHashSecret)
        {
            baseUrl = (baseUrl ?? string.Empty).Trim().TrimEnd('?');
            var hashData = BuildRequestHashData();
            var vnpSecureHashUpper = HmacSha512Public((vnpHashSecret ?? string.Empty).Trim(), hashData).ToUpperInvariant();
            return $"{baseUrl}?{hashData}&vnp_SecureHash={vnpSecureHashUpper}";
        }

        // -----------------------------
        // Validate signature (RAW QUERY)
        // -----------------------------
        private static string BuildRawForHashFromRawQuery(string rawQuery)
        {
            // rawQuery như "?vnp_Amount=...&vnp_TxnRef=...&vnp_SecureHash=..."
            var pairs = (rawQuery ?? string.Empty).TrimStart('?')
                .Split('&', StringSplitOptions.RemoveEmptyEntries);

            var filtered = pairs.Where(p =>
                !p.StartsWith("vnp_SecureHash=", StringComparison.OrdinalIgnoreCase) &&
                !p.StartsWith("vnp_SecureHashType=", StringComparison.OrdinalIgnoreCase));

            // Không decode/encode lại – giữ nguyên đúng như bên VNPAY gửi
            return string.Join("&", filtered);
        }

        public static bool ValidateSignatureFromRaw(string rawQuery, string receivedSecureHash, string secretKey,
                                                    out string rawForHash, out string myHashUpper)
        {
            rawForHash = BuildRawForHashFromRawQuery(rawQuery);
            myHashUpper = HmacSha512Public((secretKey ?? string.Empty).Trim(), rawForHash).ToUpperInvariant();

            return string.Equals(myHashUpper, receivedSecureHash, StringComparison.OrdinalIgnoreCase);
        }

        // -----------------------------
        // (Legacy) Validate from _responseData (ít khuyến nghị)
        // -----------------------------
        public bool ValidateSignature(string inputHash, string secretKey)
        {
            // Re-encode có thể lệch với raw – chỉ để tương thích.
            var clone = new SortedList<string, string>(_responseData, new VnPayCompare());
            clone.Remove("vnp_SecureHashType");
            clone.Remove("vnp_SecureHash");

            var rspRaw = string.Join("&",
                clone.Where(kv => !string.IsNullOrEmpty(kv.Value))
                     .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                     .Select(kv => $"{kv.Key}={UrlEncodeVNPay(kv.Value)}"));

            var myChecksumUpper = HmacSha512Public((secretKey ?? string.Empty).Trim(), rspRaw).ToUpperInvariant();
            return string.Equals(myChecksumUpper, inputHash, StringComparison.OrdinalIgnoreCase);
        }

        // -----------------------------
        // Hash function (public for reuse)
        // -----------------------------
        public static string HmacSha512Public(string key, string inputData)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key ?? string.Empty);
            var inputBytes = Encoding.UTF8.GetBytes(inputData ?? string.Empty);

            using var hmac = new HMACSHA512(keyBytes);
            var hash = hmac.ComputeHash(inputBytes);

            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }
    }

    public class VnPayCompare : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            if (x == y) return 0;
            if (x == null) return -1;
            if (y == null) return 1;
            var vnpCompare = CompareInfo.GetCompareInfo("en-US");
            return vnpCompare.Compare(x, y, CompareOptions.Ordinal);
        }
    }
}
