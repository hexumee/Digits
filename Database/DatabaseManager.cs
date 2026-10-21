using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OtpNet;
using Digits.Security;

namespace Digits {
    internal enum HotpCounterUpdateResult {
        Updated,
        NotFound,
        Cancelled
    }

    internal sealed class ServiceImportResult {
        public int ImportedCount { get; set; }
        public int DuplicateCount { get; set; }
        public int InvalidCount { get; set; }
        public bool Cancelled { get; set; }
    }

    internal sealed class DatabaseManager {
        public async Task<List<DatabaseElement>> GetServicesAsync(bool decryptSecrets, Func<bool> isCancelled = null) {
            List<DatabaseElement> services;
            using (var db = new DatabaseContext()) {
                services = await db.RegisteredServices.AsNoTracking().ToListAsync();
            }

            try {
                if (IsCancelled(isCancelled)) {
                    ClearSecrets(services);
                    return null;
                }

                if (decryptSecrets) {
                    foreach (var service in services) {
                        service.Secret = await TokenProtection.UnprotectSecretAsync(service.ProtectedSecret);

                        if (IsCancelled(isCancelled)) {
                            ClearSecrets(services);
                            return null;
                        }
                    }
                }

                return services;
            } catch {
                ClearSecrets(services);
                throw;
            }
        }

        public async Task<List<DatabaseElement>> GetServicesForBackupAsync(Func<bool> isCancelled = null) {
            List<DatabaseElement> services;
            using (var db = new DatabaseContext()) {
                services = await db.RegisteredServices.AsNoTracking().OrderBy(service => service.Issuer).ThenBy(service => service.Label).ToListAsync();
            }

            try {
                foreach (var service in services) {
                    if (IsCancelled(isCancelled)) {
                        ClearSecrets(services);
                        return null;
                    }

                    service.Secret = await TokenProtection.UnprotectSecretAsync(service.ProtectedSecret);
                }

                return IsCancelled(isCancelled) ? ClearAndReturnNull(services) : services;
            } catch {
                ClearSecrets(services);
                throw;
            }
        }

        public async Task<bool> TryAddTokenAsync(DatabaseElement service) {
            if (service == null) {
                return false;
            }

            try {
                string identity = TokenProtection.GetIdentity(service);

                using (var db = new DatabaseContext()) {
                    var existingServices = await db.RegisteredServices.AsNoTracking().ToListAsync();

                    try {
                        foreach (var existing in existingServices) {
                            existing.Secret = await TokenProtection.UnprotectSecretAsync(existing.ProtectedSecret);

                            if (string.Equals(TokenProtection.GetIdentity(existing), identity, StringComparison.Ordinal)) {
                                return false;
                            }
                        }
                    } finally {
                        ClearSecrets(existingServices);
                    }

                    service.ProtectedSecret = await TokenProtection.ProtectSecretAsync(service.Secret);
                    service.Secret = null;

                    db.RegisteredServices.Add(service);
                    await db.SaveChangesAsync();

                    return true;
                }
            } finally {
                service.Secret = null;
            }
        }

        public async Task<HotpCounterUpdateResult> AdvanceHotpCounterAsync(int serviceId, int newCounter, Func<bool> isCancelled = null) {
            if (IsCancelled(isCancelled)) {
                return HotpCounterUpdateResult.Cancelled;
            }

            using (var db = new DatabaseContext()) {
                var service = await db.RegisteredServices.FirstOrDefaultAsync(entry => entry.Id == serviceId);

                if (IsCancelled(isCancelled)) {
                    return HotpCounterUpdateResult.Cancelled;
                }

                if (service == null) {
                    return HotpCounterUpdateResult.NotFound;
                }

                service.Counter = newCounter;

                if (IsCancelled(isCancelled)) {
                    return HotpCounterUpdateResult.Cancelled;
                }

                await db.SaveChangesAsync();

                return IsCancelled(isCancelled) ? HotpCounterUpdateResult.Cancelled : HotpCounterUpdateResult.Updated;
            }
        }

        public async Task<DatabaseElement> GetServiceAsync(int serviceId) {
            using (var db = new DatabaseContext()) {
                return await db.RegisteredServices.AsNoTracking().FirstOrDefaultAsync(entry => entry.Id == serviceId);
            }
        }

        public async Task<bool> UpdateServiceAsync(int serviceId, string issuer, string label, Func<bool> isCancelled = null) {
            if (IsCancelled(isCancelled)) {
                return false;
            }

            using (var db = new DatabaseContext()) {
                var service = await db.RegisteredServices.FirstOrDefaultAsync(entry => entry.Id == serviceId);
                if (IsCancelled(isCancelled) || service == null) {
                    return false;
                }

                service.Issuer = issuer?.Trim() ?? string.Empty;
                service.Label = label?.Trim() ?? string.Empty;

                if (IsCancelled(isCancelled)) {
                    return false;
                }

                await db.SaveChangesAsync();

                return !IsCancelled(isCancelled);
            }
        }

        public async Task<bool> DeleteServiceAsync(int serviceId, Func<bool> isCancelled = null) {
            if (IsCancelled(isCancelled)) {
                return false;
            }

            using (var db = new DatabaseContext()) {
                var service = await db.RegisteredServices.FirstOrDefaultAsync(entry => entry.Id == serviceId);
                if (IsCancelled(isCancelled) || service == null) {
                    return false;
                }

                db.RegisteredServices.Remove(service);
                await db.SaveChangesAsync();

                return !IsCancelled(isCancelled);
            }
        }

        public async Task<ServiceImportResult> ImportServicesAsync(IList<DatabaseElement> imported, Func<bool> isCancelled = null) {
            var result = new ServiceImportResult();

            if (imported == null) {
                return result;
            }

            List<DatabaseElement> existing = null;
            try {
                using (var db = new DatabaseContext()) {
                    if (IsCancelled(isCancelled)) {
                        result.Cancelled = true;
                        return result;
                    }

                    existing = await db.RegisteredServices.AsNoTracking().ToListAsync();

                    var knownIdentities = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var item in existing) {
                        item.Secret = await TokenProtection.UnprotectSecretAsync(item.ProtectedSecret);

                        if (IsCancelled(isCancelled)) {
                            result.Cancelled = true;
                            return result;
                        }

                        knownIdentities.Add(TokenProtection.GetIdentity(item));
                    }

                    foreach (var item in imported) {
                        if (item == null) {
                            result.InvalidCount++;
                            continue;
                        }

                        NormalizeImportedItem(item);

                        if (!IsValidImportedItem(item)) {
                            result.InvalidCount++;
                            continue;
                        }

                        if (!knownIdentities.Add(TokenProtection.GetIdentity(item))) {
                            result.DuplicateCount++;
                            continue;
                        }

                        item.Id = 0;
                        item.ProtectedSecret = await TokenProtection.ProtectSecretAsync(item.Secret);

                        if (IsCancelled(isCancelled)) {
                            result.Cancelled = true;
                            return result;
                        }

                        item.Secret = null;
                        db.RegisteredServices.Add(item);
                        result.ImportedCount++;
                    }

                    if (IsCancelled(isCancelled)) {
                        result.Cancelled = true;
                        return result;
                    }

                    await db.SaveChangesAsync();
                }

                result.Cancelled = IsCancelled(isCancelled);

                return result;
            } finally {
                ClearSecrets(existing);

                foreach (var item in imported) {
                    if (item != null) {
                        item.Secret = null;
                        item.ProtectedSecret = null;
                    }
                }
            }
        }

        private static void NormalizeImportedItem(DatabaseElement item) {
            item.Type = string.IsNullOrWhiteSpace(item.Type) ? "totp" : item.Type.Trim().ToLowerInvariant();
            item.Label = item.Label?.Trim() ?? string.Empty;
            item.Secret = item.Secret?.Trim().Replace(" ", string.Empty);
            item.Issuer = item.Issuer?.Trim() ?? string.Empty;
            item.Algorithm = string.IsNullOrWhiteSpace(item.Algorithm) ? "SHA1" : item.Algorithm.Trim().ToUpperInvariant();
            item.Digits = item.Digits > 0 ? item.Digits : 6;
            item.Period = item.Period > 0 ? item.Period : 30;
        }

        private static bool IsValidImportedItem(DatabaseElement item) {
            if ((item.Type != "totp" && item.Type != "hotp") ||
                (item.Algorithm != "SHA1" && item.Algorithm != "SHA256" && item.Algorithm != "SHA512") ||
                string.IsNullOrWhiteSpace(item.Secret) ||
                item.Digits < 1 || item.Digits > 10 ||
                item.Period < 1 || item.Counter < 0)
            {
                return false;
            }

            try {
                return Base32Encoding.ToBytes(item.Secret).Length > 0;
            } catch {
                return false;
            }
        }

        private static bool IsCancelled(Func<bool> isCancelled) {
            return isCancelled != null && isCancelled();
        }

        private static void ClearSecrets(IEnumerable<DatabaseElement> items) {
            if (items == null) {
                return;
            }

            foreach (var item in items) {
                if (item != null) {
                    item.Secret = null;
                }
            }
        }

        private static List<DatabaseElement> ClearAndReturnNull(List<DatabaseElement> services) {
            ClearSecrets(services);
            return null;
        }
    }
}
