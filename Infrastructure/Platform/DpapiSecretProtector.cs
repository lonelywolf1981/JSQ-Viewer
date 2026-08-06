using System;
using System.Security.Cryptography;
using System.Text;
using JSQViewer.Application.Abstractions;

namespace JSQViewer.Infrastructure.Platform
{
    public sealed class DpapiSecretProtector : ISecretProtector
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("JSQViewer.DatabaseConnection.v1");

        public string Protect(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                return string.Empty;
            }

            byte[] encrypted = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(plainText), Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }

        public string Unprotect(string protectedText)
        {
            if (string.IsNullOrEmpty(protectedText))
            {
                return string.Empty;
            }

            byte[] decrypted = ProtectedData.Unprotect(
                Convert.FromBase64String(protectedText), Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
    }
}
