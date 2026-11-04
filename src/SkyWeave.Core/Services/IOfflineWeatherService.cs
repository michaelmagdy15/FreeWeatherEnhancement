using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SkyWeave.Core.Models;

namespace SkyWeave.Core.Services;

public interface IOfflineWeatherService
{
    bool IsOfflineMode { get; }
    OfflineWeatherPackage? ActivePackage { get; }
    string StorageDirectory { get; }

    IReadOnlyList<OfflinePackageSummary> ListPackages();
    Task<OfflineWeatherPackage?> GetPackageAsync(string packageId);
    Task<OfflineWeatherPackage> SavePackageAsync(OfflineWeatherPackage package, CancellationToken ct = default);
    bool DeletePackage(string packageId);

    Task<OfflineWeatherPackage> CreatePackageAsync(OfflinePackageCreateRequest request, CancellationToken ct = default);
    OfflineWeatherPackage CreateSyntheticBaselinePackage(string preset = "egypt");
    Task<bool> ActivatePackageAsync(string packageId);
    void DeactivateOfflineMode();
    void SetActivePackageDirect(OfflineWeatherPackage package);
}
