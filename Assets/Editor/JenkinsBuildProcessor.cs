#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

public class JenkinsBuildProcessor
{
    public const string AddressableBuilderName = "BattleLeague Addressable Build";
    private static bool _isJenkinsBuild = false;

    private class EditorCoroutine
    {
        private IEnumerator routine;

        private EditorCoroutine(IEnumerator routine)
        {
            this.routine = routine;
        }

        public static EditorCoroutine Start(IEnumerator routine)
        {
            EditorCoroutine coroutine = new EditorCoroutine(routine);
            coroutine.Start();
            return coroutine;
        }

        private void Start()
        {
            EditorApplication.update += Update;
        }

        public void Stop()
        {
            EditorApplication.update -= Update;
        }

        private void Update()
        {
            if (!routine.MoveNext())
            {
                Stop();
            }

            JenkinsBuildProcessor.BuildAnd();
        }
    }

    [MenuItem("Tools/CI/Build iOS Debug")]
    public static void BuildIOS()
    {
        // 1. Build Number Automatically Up
        AddBuildVersion(BuildTarget.iOS);
        BuildOptions opt = BuildOptions.None;
        GenericBuild(FindEnabledEditorScenes(), $"./Build/IOS/", BuildTarget.iOS, opt);
    }

    [MenuItem("Tools/CI/Build And Debug")]
    public static void BuildAnd()
    {
        Console.WriteLine("Beginning BuildAnd");

        // 1. Build Number Automatically Up
        //var newBuildVersionCode = AddBuildVersion(BuildTarget.Android);

        //PlayerSettings.Android.useCustomKeystore = true;
        // PlayerSettings.Android.keystoreName = "AndroidKey.keystore";
        //PlayerSettings.Android.keystorePass = "**KeyStore Password**";
        //PlayerSettings.Android.keyaliasName = "**Alias name**";
        //PlayerSettings.Android.keyaliasPass = "**Alias password**";
        PlayerSettings.Android.useCustomKeystore = false;

        BuildOptions opt = BuildOptions.None;
        _isJenkinsBuild = true;
        GenericBuild(FindEnabledEditorScenes(), $"./Build/Android/", BuildTarget.Android, opt);
        Console.WriteLine("End Of BuildAnd");
    }

    private static string AddBuildVersion(BuildTarget buildTarget)
    {
        CheckBuildTarget(buildTarget);

        string oldBuildNumber;

        switch (buildTarget)
        {
            case BuildTarget.Android:
                oldBuildNumber = PlayerSettings.Android.bundleVersionCode.ToString();
                PlayerSettings.Android.bundleVersionCode = (int.Parse(oldBuildNumber) + 1);
                return PlayerSettings.Android.bundleVersionCode.ToString();
            case BuildTarget.iOS:
                oldBuildNumber = PlayerSettings.iOS.buildNumber;
                PlayerSettings.iOS.buildNumber = (int.Parse(oldBuildNumber) + 1).ToString();
                return PlayerSettings.iOS.buildNumber;
        }

        return "null";
    }

    private static void CheckBuildTarget(BuildTarget buildTarget)
    {
        if (buildTarget == BuildTarget.Android || buildTarget == BuildTarget.iOS)
            return;

        throw new Exception($"Is not supported Platform, {buildTarget}");
    }

    private static void GenericBuild(string[] scenes, string targetPath, BuildTarget buildTarget,
        BuildOptions buildOptions)
    {
        //copy all table files 
        string path = Environment.CurrentDirectory + "../data/" + "CopyAll.bat";
        Debug.Log(path);
        Debug.Log(Environment.CurrentDirectory);

        System.Diagnostics.Process process = new System.Diagnostics.Process();
        System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
        startInfo.FileName = "cmd.exe";
        startInfo.CreateNoWindow = true;
        //startInfo.WorkingDirectory = @"C:\";
        startInfo.Arguments = "/c cd .. & cd data & CopyAll.bat";
        process.StartInfo = startInfo;
        process.Start();
        process.WaitForExit();

        // build
        if (File.Exists(targetPath) == false)
            Directory.CreateDirectory(targetPath);

        var buildTargetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);
        EditorUserBuildSettings.SwitchActiveBuildTarget(buildTargetGroup, buildTarget);


        string timestring = string.Format("{0}_{1}",
            DateTime.Now.ToString("yyyy") + DateTime.Now.ToString("MM") + DateTime.Now.ToString("dd"),
            DateTime.Now.ToString("HH") + DateTime.Now.ToString("mm") + DateTime.Now.ToString("ss"));
        string target = targetPath + string.Format("/{0}_{1}_{2}.apk", PlayerSettings.productName,
            PlayerSettings.bundleVersion, timestring);
        
        BuildPipeline.BuildPlayer(scenes, target, buildTarget, buildOptions);
    }

    private static string[] FindEnabledEditorScenes()
    {
        List<string> EditorScenes = new List<string>();
        foreach (var scene in EditorBuildSettings.scenes)
        {
            if (!scene.enabled)
                continue;

            EditorScenes.Add(scene.path);
        }

        return EditorScenes.ToArray();
    }

    [PostProcessBuild(100)]
    public static void OnAndroidBuildFinish(BuildTarget target, string pathToBuildProject)
    {
        if (!_isJenkinsBuild)
            return;

        Console.WriteLine("Android Build Finish");
        if (target == BuildTarget.Android)
        {
            EditorCoroutine.Start(UploadApkAsync(pathToBuildProject));
        }

        _isJenkinsBuild = false;
    }

    /// <summary>
    /// -CustomArgs:Language=en_US;Version=1.02
    /// NOTE: https://waraccc.tistory.com/8
    /// NOTE: https://github.com/Elringus/UnityGoogleDrive
    /// </summary>
    /// <param name="pathToBuildProject"></param>
    /// <returns></returns>
    private static IEnumerator UploadApkAsync(string pathToBuildProject)
    {
        // string parentId = CommandLineReader.GetCustomArgument("ParentId");
        // if (string.IsNullOrEmpty(parentId))
        // {
        //     Debug.Log("Stop UploadApkAsync :" + parentId);
        //     yield break;
        // }
        //
        // if (File.Exists(pathToBuildProject))
        // {
        //     var apkName = pathToBuildProject.Split('/').Last(); // apk 파일 이름 얻기
        //     var apk = new UnityGoogleDrive.Data.File
        //     {
        //         Name = apkName, Content = File.ReadAllBytes(pathToBuildProject),
        //         Parents = new List<string> { parentId, }
        //     };
        //     var req = UnityGoogleDrive.GoogleDriveFiles.Create(apk);
        //     req.OnDone += response =>
        //     {
        //         if (req.IsError) Debug.LogError(req.Error);
        //         if (req.IsDone)
        //         {
        //             Debug.Log("Upload Success!! " + apkName);
        //             Debug.Log(response.WebViewLink);
        //             if (!string.IsNullOrEmpty(response.WebViewLink))
        //                 Application.OpenURL(req.ResponseData.WebViewLink);
        //         }
        //     };
        //
        //     yield return req.Send(); //upload
        //
        //     while (!req.IsDone)
        //         yield return 0;
        //
        //     Debug.Log("End Of Upload Process");
        // }
        // else
        // {
        //     Console.WriteLine("Error UploadApk " + pathToBuildProject);
        //     Debug.Log($"Built File Path:{pathToBuildProject} File Not Exists");
        // }

        yield return null;
        EditorApplication.Exit(0);
    }
}
#endif