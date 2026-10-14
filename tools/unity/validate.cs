var issues = new System.Collections.Generic.List<UnityEngine.XR.OpenXR.Features.OpenXRFeature.ValidationRule>();
UnityEditor.XR.OpenXR.OpenXRProjectValidation.GetCurrentValidationIssues(issues, UnityEditor.BuildTargetGroup.Android);
var result = new System.Text.StringBuilder();
result.AppendLine("OpenXR Android issues: " + issues.Count);
foreach (var issue in issues) result.AppendLine(issue.message);
var settings = UnityEngine.XR.OpenXR.OpenXRSettings.GetSettingsForBuildTargetGroup(UnityEditor.BuildTargetGroup.Android);
foreach (var feature in settings.GetFeatures())
    if (feature.enabled) result.AppendLine("Enabled: " + feature.GetType().FullName);
result.AppendLine("Compiling: " + UnityEditor.EditorApplication.isCompiling);
System.IO.Directory.CreateDirectory("docs/evidence");
System.IO.File.WriteAllText("docs/evidence/openxr.txt",result.ToString());
return result.ToString();
