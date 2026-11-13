using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ItchDeploy.Editor
{
    public static class ItchDeployReport
    {
        [Serializable]
        private class ReportData
        {
            public string project;
            public string version;
            public string platform;
            public string channel;
            public string date;
            public double buildTimeSeconds;
            public double sizeMB;
            public bool success;
            public string unityVersion;
            public List<string> errors;
        }

        public static bool AutoGenerate
        {
            get => EditorPrefs.GetBool("ItchDeploy_AutoReport", true);
            set => EditorPrefs.SetBool("ItchDeploy_AutoReport", value);
        }

        public static string LastReportDir
        {
            get
            {
                var d = EditorPrefs.GetString("ItchDeploy_ReportDir", "");
                if (string.IsNullOrEmpty(d))
                    d = Path.Combine(Application.dataPath, "..", "BuildReports");
                return d;
            }
            set => EditorPrefs.SetString("ItchDeploy_ReportDir", value);
        }

        public static string GenerateJson(string platform, string channel, string version,
            double buildTime, double sizeMB, bool success, List<string> errors)
        {
            var report = new ReportData
            {
                project = Application.productName,
                version = version,
                platform = platform,
                channel = channel,
                date = DateTime.Now.ToString("O"),
                buildTimeSeconds = buildTime,
                sizeMB = sizeMB,
                success = success,
                unityVersion = Application.unityVersion,
                errors = errors ?? new List<string>(),
            };

            return JsonUtility.ToJson(report, true);
        }

        public static void SaveReport(string platform, string channel, string version,
            double buildTime, double sizeMB, bool success, List<string> errors)
        {
            var dir = LastReportDir;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var jsonPath = Path.Combine(dir, $"build_{platform}_{timestamp}.json");
            var htmlPath = Path.Combine(dir, $"build_{platform}_{timestamp}.html");

            var json = GenerateJson(platform, channel, version, buildTime, sizeMB, success, errors);
            File.WriteAllText(jsonPath, json);

            var html = GenerateHtml(json);
            File.WriteAllText(htmlPath, html);

            Debug.Log($"[BuildReport] Saved: {jsonPath}");
            Debug.Log($"[BuildReport] Saved: {htmlPath}");
        }

        private static string HtmlEscape(string value)
            => (value ?? string.Empty)
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");

        private static string GenerateHtml(string jsonData)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            var escaped = jsonData.Replace("<", "\\u003c");

            return
                "<!DOCTYPE html>\n" +
                "<html><head><meta charset='utf-8'>\n" +
                "<title>Build Report</title>\n" +
                "<style>\n" +
                "body{font-family:-apple-system,sans-serif;max-width:800px;margin:40px auto;padding:0 20px;background:#fafafa}\n" +
                "h1{color:#c34}\n" +
                ".card{background:#fff;border-radius:8px;padding:20px;margin:12px 0;box-shadow:0 2px 8px rgba(0,0,0,0.08)}\n" +
                ".label{color:#888;font-size:12px;text-transform:uppercase;letter-spacing:0.5px}\n" +
                ".value{font-size:18px;font-weight:600;margin:4px 0}\n" +
                ".success{color:#090}\n" +
                ".fail{color:#c00}\n" +
                "pre{background:#f0f0f0;padding:12px;border-radius:4px;overflow-x:auto}\n" +
                "</style></head><body>\n" +
                $"<h1>Build Report: {HtmlEscape(Application.productName)}</h1>\n" +
                $"<p>{timestamp}</p>\n" +
                "<script id='data' type='application/json'>" + escaped + "</script>\n" +
                "<div id='content'></div>\n" +
                "<script>\n" +
                "var d=JSON.parse(document.getElementById('data').textContent);\n" +
                "var c=document.getElementById('content');\n" +

                "function card(label,value,valueClass,pre){\n" +
                "  var box=document.createElement('div'); box.className='card';\n" +
                "  var l=document.createElement('div'); l.className='label'; l.textContent=label;\n" +
                "  var v=document.createElement(pre?'pre':'div');\n" +
                "  if(!pre){v.className='value'+(valueClass?' '+valueClass:'');}\n" +
                "  v.textContent=value;\n" +
                "  box.appendChild(l); box.appendChild(v); c.appendChild(box);\n" +
                "}\n" +
                "card('Version', d.version);\n" +
                "card('Platform', d.platform+' / '+d.channel);\n" +
                "card('Status', d.success?'SUCCESS':'FAILED', d.success?'success':'fail');\n" +
                "card('Build Time', d.buildTimeSeconds+'s');\n" +
                "card('Size', d.sizeMB+' MB');\n" +
                "card('Unity', d.unityVersion);\n" +
                "if(d.errors && d.errors.length){ card('Errors', d.errors.join('\\n'), null, true); }\n" +
                "</script></body></html>";
        }
    }
}
