using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.CrashReportHandler;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.XR.Management;

public class JNIStorage : MonoBehaviour
{
    public static AndroidJavaClass apiClass;
    public static AndroidJavaObject accountObj;
    public static AndroidJavaObject activity;
    public static AndroidJavaObject instancesObj;
    public static ConnectionStatus connectionStatus;
    public APIHandler apiHandler;
    public static JNIStorage instance;
    public List<string> supportedVersions;
    public UIHandler uiHandler;
    public TMP_Dropdown instancesDropdown;
    public ConfigHandler configHandler;
    public GameObject instancePrefab;
    public GameObject instanceArray;

    public enum ConnectionStatus
    {
        Connected,
        Disconnected
    }

    public static bool CheckConnectionAndThrow()
    {
        switch (connectionStatus)
        {
            case ConnectionStatus.Disconnected:
                return false;
            case ConnectionStatus.Connected:
            default:
                return true;
        }
    }

    static void CloseXR()
    {
        XRGeneralSettings.Instance.Manager.activeLoader.Stop();
        XRGeneralSettings.Instance.Manager.activeLoader.Deinitialize();
    }
    
    [RuntimeInitializeOnLoadMethod]
    static void RunOnStart()
    {
        Application.unloading += CloseXR;
    }

    private void Start()
    {
		CrashReportHandler.enableCaptureExceptions = false;
        instance = this;

        // If the user has not granted Microphone permission, don't try to start the clip.
        if (Microphone.devices.Length != 0)
        {
            Microphone.Start(Microphone.devices[0], true, 1, 44100);
        }

        apiClass = new AndroidJavaClass("pojlib.API");
        instancesObj = apiClass.CallStatic<AndroidJavaObject>("loadAll");
        configHandler.LoadConfig();
        UpdateInstances();
	    apiClass.SetStatic("model", OpenXRFeatureSystemInfo.GetHeadsetName());
        
        CheckConnection();
    }

    private void FillSupportedVersions(string[] supportedVersionsArray)
    {
        supportedVersions.Clear();
        supportedVersions.AddRange(supportedVersionsArray);
        AndroidJavaObject[] instances = instancesObj.Call<AndroidJavaObject[]>("toArray");
        foreach (var instanceObj in instances)
        {
            string name = instanceObj.Get<string>("instanceName");
            string image = instanceObj.Get<string>("instanceImageURL");
            
            if (!supportedVersions.Contains(name))
            {
                supportedVersions.Add(name);
            }
        }
    }

    public static PojlibInstance GetInstance(string name)
    {
        AndroidJavaObject[] instances = instancesObj.Call<AndroidJavaObject[]>("toArray");
        foreach (var instanceObj in instances)
        {
            PojlibInstance pojlibInstance = PojlibInstance.Parse(instanceObj);
            if (pojlibInstance.instanceName.Equals(name))
            {
                return pojlibInstance;
            }
        }

        return null;
    }

    public void UpdateInstances()
    {
        if (Application.platform != RuntimePlatform.Android) return;
        AndroidJavaClass jc = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        activity = jc.GetStatic<AndroidJavaObject>("currentActivity");
        uiHandler.ClearDropdowns();
        string[] supportedVersionsArray = apiClass.CallStatic<string[]>("getQCSupportedVersions");
        FillSupportedVersions(supportedVersionsArray);
        uiHandler.UpdateDropdowns(true, supportedVersions);
    }
    
    public static async void CheckConnection()
    {
        UnityWebRequest request = UnityWebRequest.Get("https://piston-meta.mojang.com/mc/game/version_manifest_v2.json");
        request.SendWebRequest();
        
        while (!request.isDone)
            await Task.Delay(16);
        
        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.Log("Unable to contact Mojang servers" + request.error);
            connectionStatus = ConnectionStatus.Disconnected;
        }

        connectionStatus = ConnectionStatus.Connected;
    }
}
