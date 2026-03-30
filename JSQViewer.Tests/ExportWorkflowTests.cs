using System;
using System.Collections.Generic;
using JSQViewer.Application.Exporting;
using JSQViewer.Application.Exporting.Ports;
using JSQViewer.Presentation.WinForms.Presenters;
using JSQViewer.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSQViewer.Tests
{
    [TestClass]
    public class ExportTemplateUseCaseTests
    {
        [TestMethod]
        public void Execute_UsesExporterThenValidatorAndReturnsPayload()
        {
            var exporter = new FakeTemplateExporter();
            var validator = new FakeTemplateExportValidator { Result = new TemplateValidationResult { Ok = true, Message = "ok" } };
            var useCase = new ExportTemplateUseCase(exporter, validator);
            var request = new ExportTemplateRequest
            {
                TemplatePath = "template.xlsx",
                LoadedFolder = "C:\\tests\\root",
                Data = SessionAndChartingTestData.CreateData(new long[] { 0L, 1000L }),
                SelectedChannels = new[] { "A-01" },
                Refrigerant = "R290",
                ViewerSettings = ViewerSettingsModel.CreateDefault()
            };

            ExportTemplateResult result = useCase.Execute(request);

            Assert.AreEqual(1, exporter.CallCount);
            Assert.AreEqual(1, validator.CallCount);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, result.Payload);
            Assert.IsTrue(result.Validation.Ok);
        }
    }

    [TestClass]
    public class ExportSettingsPresenterTests
    {
        [TestMethod]
        public void BuildRequest_ConvertsSelectedDateRangeIntoUnixMilliseconds()
        {
            var presenter = new ExportSettingsPresenter();
            double startOa = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Local).ToOADate();
            double endOa = new DateTime(2024, 1, 1, 11, 0, 0, DateTimeKind.Local).ToOADate();

            ExportTemplateRequest request = presenter.BuildRequest(
                "template.xlsx",
                "C:\\tests\\root",
                SessionAndChartingTestData.CreateData(new long[] { 0L, 1000L }),
                new[] { "A-01" },
                includeExtra: true,
                refrigerant: "R600a",
                viewerSettings: ViewerSettingsModel.CreateDefault(),
                overlayMode: false,
                rangeStartOa: startOa,
                rangeEndOa: endOa);

            Assert.IsTrue(request.RangeStartMs.HasValue);
            Assert.IsTrue(request.RangeEndMs.HasValue);
            Assert.IsTrue(request.RangeEndMs.Value > request.RangeStartMs.Value);
        }
    }

    [TestClass]
    public class ViewerSettingsSanitizerTests
    {
        [TestMethod]
        public void Sanitize_NormalizesInvalidScaleBoundsAndColors()
        {
            var sanitizer = new ViewerSettingsSanitizer();
            var settings = ViewerSettingsModel.CreateDefault();
            settings.row_mark.color = "bad";
            settings.scales["W"] = new ScaleSettings
            {
                min = 10,
                opt = 5,
                max = 1,
                colors = new ScaleColors { min = "bad", opt = "#00ff00", max = string.Empty }
            };

            ViewerSettingsModel sanitized = sanitizer.Sanitize(settings);

            Assert.AreEqual("#EAD706", sanitized.row_mark.color);
            Assert.IsTrue(sanitized.scales["W"].min < sanitized.scales["W"].opt);
            Assert.IsTrue(sanitized.scales["W"].opt <= sanitized.scales["W"].max);
            Assert.AreEqual("#1CBCF2", sanitized.scales["W"].colors.min);
            Assert.AreEqual("#00FF00", sanitized.scales["W"].colors.opt);
            Assert.AreEqual("#F3919B", sanitized.scales["W"].colors.max);
        }
    }

    internal sealed class FakeTemplateExporter : ITemplateExporter
    {
        public int CallCount { get; private set; }

        public byte[] Export(ExportTemplateRequest request)
        {
            CallCount++;
            return new byte[] { 1, 2, 3 };
        }
    }

    internal sealed class FakeTemplateExportValidator : ITemplateExportValidator
    {
        public int CallCount { get; private set; }

        public TemplateValidationResult Result { get; set; }

        public TemplateValidationResult Validate(byte[] xlsxBytes)
        {
            CallCount++;
            return Result;
        }
    }
}
