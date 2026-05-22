using System.Reflection;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NotesApi.Controllers;

/// <summary>
/// Returns version metadata for all services. Available at /api/version (no auth required).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/version")]
public class VersionController : ControllerBase
{
    private readonly IConfiguration _config;

    public VersionController(IConfiguration config) => _config = config;

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Get()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var apiVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                             ?.InformationalVersion
                         ?? assembly.GetName().Version?.ToString()
                         ?? "unknown";

        return Ok(new
        {
            services = new[]
            {
                new ServiceVersionInfo("api",    "ASP.NET Core 8",     apiVersion,     "1.0"),
                new ServiceVersionInfo("ui",     "Nuxt 4 / Vue 3",     GetEnvVersion("UI_VERSION",     "2.0.0"), "1.0"),
                new ServiceVersionInfo("mobile", "Nuxt 3 / Ionic 8",   GetEnvVersion("MOBILE_VERSION", "0.1.0"), "1.0"),
                new ServiceVersionInfo("llm",    "FastAPI / Ollama",   GetEnvVersion("LLM_VERSION",    "0.1.0"), "1.0"),
            },
            supportedApiVersions = new[] { "1.0" },
            deprecatedApiVersions = Array.Empty<string>(),
            timestamp = DateTime.UtcNow,
        });
    }

    private string GetEnvVersion(string key, string fallback) =>
        _config[key] ?? Environment.GetEnvironmentVariable(key) ?? fallback;
}

internal record ServiceVersionInfo(string Service, string Tech, string Version, string ApiVersion);
