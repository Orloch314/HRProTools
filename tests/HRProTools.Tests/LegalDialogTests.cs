using System.Net;
using HRProTools.Web.Components.Shared;
using HRProTools.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace HRProTools.Tests;

public sealed partial class Phase4VacationRequestTests
{
    private sealed class PrerenderOnlyJs : IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) => throw new InvalidOperationException("No JS is executed during static rendering.");
        public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken token, object?[]? args) => InvokeAsync<T>(identifier, args);
    }
    [Fact]
    public async Task LegalDialogAndDurationRenderStructuredPublishedValues()
    {
        await using var provider = new ServiceCollection().AddLogging().AddSingleton<IJSRuntime>(new PrerenderOnlyJs()).BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var rendered = new List<string>();
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            foreach (var original in LegalPermissionCatalog.All)
            {
                var rule = original.Protected || original.Code == "REPOSO_MEDICO" ? original : original with { Engine = "PARAMETRIC", IsPaid = original.Code != "PERMISO_SIN_GOCE" };
                var output = await renderer.RenderComponentAsync<LegalPermissionDialog>(ParameterView.FromDictionary(new Dictionary<string, object?> { { "Rule", rule }, { "Version", 7 }, { "TriggerId", "opener" } }));
                var html = output.ToHtmlString(); var text = WebUtility.HtmlDecode(html);
                Assert.Contains("Ficha del permiso legal", text); Assert.Contains("Versión 7", text); Assert.Contains(rule.Name, text); Assert.Contains("Duración y límites legales", text); Assert.Contains("aria-labelledby=", html); Assert.Contains("data-dialog-close", html);
                Assert.DoesNotContain("Sin máximo simple", text); Assert.DoesNotContain("EVENT_DAYS", text); Assert.DoesNotContain("CALENDAR_DAYS", text); Assert.DoesNotContain("RECURRENT_MINUTES", text);
                if (rule.Code == "MATRIMONIO") Assert.Contains($"{LegalPermissionLimits.EventDays} días", text);
                if (rule.Code == "PATERNIDAD") Assert.Contains($"{LegalPermissionLimits.PaternityWeeks} semanas", text);
                if (rule.Code == "MATERNIDAD") { Assert.Contains($"{LegalPermissionLimits.MaternityWeeks} semanas", text); Assert.Contains($"{LegalPermissionLimits.SpecialMaternityWeeks} semanas", text); }
                if (rule.Code is "MATRIMONIO" or "PERMISO_ORDINARIO_CON_GOCE") rendered.Add(html);
            }
            var custom = TestRule("HOURS") with { MaximumQuantity = 6, AnnualLimit = 12 }; var outputCustom = await renderer.RenderComponentAsync<LegalRuleDetails>(ParameterView.FromDictionary(new Dictionary<string, object?> { { "Rule", custom } }));
            var customText = WebUtility.HtmlDecode(outputCustom.ToHtmlString()); Assert.Contains("6 horas", customText); Assert.Contains("12 horas", customText);
            var balance = new PermissionQuotaSummary("TEST", "Prueba", "99", "Persona sintética", [new("YEAR", "Año 2026", "DAYS", 2, 1, 1, 0)], [], false);
            var quota = await renderer.RenderComponentAsync<PermissionQuotaPanel>(ParameterView.FromDictionary(new Dictionary<string, object?> { { "Summary", balance } })); var quotaText = WebUtility.HtmlDecode(quota.ToHtmlString()); Assert.Contains("Reservados", quotaText); Assert.Contains("No queda cupo", quotaText); Assert.Contains("Disponibles", quotaText);
        });
        var evidence = Environment.GetEnvironmentVariable("HRPROTOOLS_UI_EVIDENCE_DIR");
        if (!string.IsNullOrWhiteSpace(evidence))
        {
            Directory.CreateDirectory(evidence);
            var marriage = rendered[0].Replace("<dialog ", "<dialog id=\"marriage-dialog\" ");
            var ordinary = rendered[1].Replace("<dialog ", "<dialog id=\"ordinary-dialog\" ").Replace("legal-dialog-title", "ordinary-dialog-title").Replace("legal-dialog-identity", "ordinary-dialog-identity");
            var rows = string.Join("", Enumerable.Range(1, 65).Select(i => $"<tr><td>Categoría de prueba {i}</td><td>Lista larga para verificar scroll</td><td><button id='opener-{i}' data-rule='{(i == 1 ? "marriage" : "ordinary")}' class='btn-secondary'>Ficha {i}</button></td></tr>"));
            var html = $$$"""
<!DOCTYPE html><html lang="es"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><link rel="stylesheet" href="/app.css"><link rel="stylesheet" href="/HRProTools.Web.styles.css"><title>Prueba de ficha legal</title><style>body{padding:0 24px}header{position:sticky;top:0;background:#08243c;color:white;padding:20px;z-index:2000}main{max-width:1000px;margin:auto}td{padding:16px}.probe-links{display:flex;gap:20px;margin:20px 0}</style></head><body><header>HR ProTools · Verificación con componentes reales y datos sintéticos</header><main><h1>Catálogo de prueba</h1><p>Las fichas siguientes están renderizadas por los componentes Blazor reales. El cierre usa su módulo JavaScript original.</p><div class="probe-links"><a href="/">Escritorio</a><a href="/mobile">Móvil 375 px</a></div><table><tbody>{{{rows}}}</tbody></table></main>{{{marriage}}}{{{ordinary}}}<script type="module">import { show,close,dispose } from '/Components/Shared/LegalPermissionDialog.razor.js'; document.querySelectorAll('[data-rule]').forEach(button=>button.addEventListener('click',()=>{const dialog=document.getElementById(button.dataset.rule+'-dialog');show(dialog,button.id,{invokeMethodAsync:()=>{dispose(dialog);return Promise.resolve()}})}));document.querySelectorAll('[data-dialog-close]').forEach(button=>button.addEventListener('click',()=>close(button.closest('dialog'))));</script></body></html>
""";
            await File.WriteAllTextAsync(Path.Combine(evidence, "index.html"), html);
        }
    }
}
