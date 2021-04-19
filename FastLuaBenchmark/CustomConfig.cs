using BenchmarkDotNet.Analysers;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Filters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Validators;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastLuaBenchmark
{
    internal sealed class CustomConfig : IConfig
    {
        private class Logger : ILogger
        {
            private bool _emptyLine;
            private bool _statistics;

            public void Write(LogKind logKind, string text)
            {
                if (logKind == LogKind.Default || logKind == LogKind.Info)
                {
                    if (_statistics && logKind == LogKind.Statistic)
                    {
                        Console.WriteLine(text);
                    }
                    return;
                }
                if (logKind == LogKind.Statistic)
                {
                    _statistics = true;
                }
                Console.Write(text);
            }

            public void WriteLine()
            {
                if (_statistics) Console.WriteLine();
            }

            public void WriteLine(LogKind logKind, string text)
            {
                if (text.StartsWith("// * "))
                {
                    text = text + "\r\n";
                }
                if (logKind == LogKind.Default || logKind == LogKind.Statistic ||
                    logKind == LogKind.Info || logKind == LogKind.Header)
                {
                    if (logKind == LogKind.Header && text.StartsWith("// * "))
                    {
                        Console.WriteLine(text);
                        return;
                    }
                    if (logKind == LogKind.Header && text.StartsWith("// ***** "))
                    {
                        if (text.Contains("Finish"))
                        {
                            Console.WriteLine();
                            Console.WriteLine(text);
                            Console.WriteLine();
                        }
                        else
                        {
                            Console.WriteLine(text);
                        }
                        return;
                    }
                    if (logKind == LogKind.Header && text.StartsWith("// **************************"))
                    {
                        if (!_emptyLine)
                        {
                            _emptyLine = true;
                            Console.WriteLine();
                        }
                        return;
                    }
                    if (logKind == LogKind.Header && text.StartsWith("// Benchmark:"))
                    {
                        Console.WriteLine(text);
                        return;
                    }
                    if (logKind == LogKind.Header && text.Contains("Legends"))
                    {
                        Console.WriteLine(text);
                        return;
                    }
                    if (_statistics && logKind == LogKind.Statistic)
                    {
                        Console.WriteLine(text);
                        return;
                    }
                    return;
                }
                Console.WriteLine(text);
            }

            public void Flush()
            {
            }

            public string Id => "custom";
            public int Priority => 0;
        }

        public IEnumerable<IColumnProvider> GetColumnProviders() => DefaultColumnProviders.Instance;

        public IEnumerable<IExporter> GetExporters()
        {
            yield break;
        }

        public IEnumerable<ILogger> GetLoggers()
        {
            yield return new Logger();
        }

        public IEnumerable<IAnalyser> GetAnalysers()
        {
            yield return EnvironmentAnalyser.Default;
            yield return OutliersAnalyser.Default;
            yield return MinIterationTimeAnalyser.Default;
            yield return MultimodalDistributionAnalyzer.Default;
            yield return RuntimeErrorAnalyser.Default;
            yield return ZeroMeasurementAnalyser.Default;
            yield return BaselineCustomAnalyzer.Default;
        }

        public IEnumerable<IValidator> GetValidators()
        {
            yield return BaselineValidator.FailOnError;
            yield return SetupCleanupValidator.FailOnError;
#if !DEBUG
            yield return JitOptimizationsValidator.FailOnError;
#endif
            yield return RunModeValidator.FailOnError;
            yield return GenericBenchmarksValidator.DontFailOnError;
            yield return DeferredExecutionValidator.FailOnError;
            yield return ParamsAllValuesValidator.FailOnError;
        }

        public IOrderer Orderer => null;

        public ConfigUnionRule UnionRule => ConfigUnionRule.Union;

        public CultureInfo CultureInfo => null;

        public ConfigOptions Options => ConfigOptions.Default;

        public SummaryStyle SummaryStyle => SummaryStyle.Default;

        public string ArtifactsPath
        {
            get
            {
                var root = Directory.GetCurrentDirectory();
                return Path.Combine(root, "BenchmarkDotNet.Artifacts");
            }
        }

        public IEnumerable<Job> GetJobs() => Array.Empty<Job>();
        public IEnumerable<BenchmarkLogicalGroupRule> GetLogicalGroupRules() => Array.Empty<BenchmarkLogicalGroupRule>();
        public IEnumerable<IDiagnoser> GetDiagnosers() => Array.Empty<IDiagnoser>();
        public IEnumerable<HardwareCounter> GetHardwareCounters() => Array.Empty<HardwareCounter>();
        public IEnumerable<IFilter> GetFilters() => Array.Empty<IFilter>();
    }
}
