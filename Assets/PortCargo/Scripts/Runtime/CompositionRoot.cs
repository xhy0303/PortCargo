using System;
using System.IO;
using PortCargo.Core;
using PortCargo.Platform;
using UnityEngine;

namespace PortCargo.Runtime
{
    /// <summary>Explicit composition only. No singleton, service locator, physics or runtime SDK in Core.</summary>
    public sealed class CompositionRoot : MonoBehaviour
    {
        [SerializeField] private TextAsset catalog;
        [SerializeField] private GameTuning tuning;
        [SerializeField] private GamePresenter presenter;
        [SerializeField] private DialogView dialog;
        private void Start()=>RetryBoot();
        public void RetryBoot()
        {
#if UNITY_WEBGL && PORT_WECHAT && !UNITY_EDITOR
            var platform=GetComponent<WeChatPlatform>();
            if(!platform)platform=gameObject.AddComponent<WeChatPlatform>();
            platform.Initialize(presenter,Boot,ShowBootError);
#else
            Boot();
#endif
        }
        private void Boot()
        {
            try
            {
                if(!catalog||!tuning||!presenter||!dialog)throw new InvalidOperationException("BOOT_REFERENCES");
                Application.targetFrameRate=tuning.TargetFps;QualitySettings.vSyncCount=0;
#if UNITY_EDITOR
                Application.runInBackground=true;
#endif
                var codec=new UnityJsonCodec();var levels=LevelValidator.Load(codec.Decode<CatalogData>(catalog.text));
                IStoragePort storage;
#if UNITY_WEBGL && !UNITY_EDITOR
                #if PORT_WECHAT
                storage=new WeChatStoragePort();
                // Surface platform access failures before repository recovery can create a new profile.
                storage.ReadString("portcargo.save.a");storage.ReadString("portcargo.save.b");
#else
                throw new PlatformNotSupportedException("Use the PortCargo WeChat export configuration.");
#endif
#else
                var directory=Path.Combine(Application.persistentDataPath,"Saves");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                var args=Environment.GetCommandLineArgs();
                for(var i=0;i<args.Length-1;i++)if(args[i]=="--portcargo-save-dir")directory=args[i+1];
#endif
                storage=new FileStoragePort(directory);
#endif
                var repository=new SaveRepository(storage,codec,value=>ValidateSave(value,levels));
                var nativePresenter=GetComponent<NativeAdsPresenter>();
                if(nativePresenter)nativePresenter.Bind(presenter.Flow,null);
                presenter.BindRewardedAds(null,null);
#if UNITY_WEBGL && PORT_WECHAT && !UNITY_EDITOR
                if(tuning.AdsEnabled)
                {
                    var ads=GetComponent<WeChatRewardedAds>();
                    if(!ads)ads=gameObject.AddComponent<WeChatRewardedAds>();
                    ads.Configure(tuning.RewardedHintAdUnitId,tuning.EndlessSkipAdUnitId);
                    presenter.BindRewardedAds(ads.Hints,ads.EndlessSkip);
                }
#endif
                presenter.Initialize(levels,tuning,repository,repository.Load());
#if UNITY_WEBGL && PORT_WECHAT && !UNITY_EDITOR
                if(tuning.NativeAdsEnabled)
                {
                    // Native cards are optional; an unavailable ad API must not become a boot failure.
                    try
                    {
                        var nativeAds=GetComponent<WeChatNativeAds>();
                        if(!nativeAds)nativeAds=gameObject.AddComponent<WeChatNativeAds>();
                        // This single-grid template does not emit size events. The conservative
                        // envelope includes its ad/close label; evidence: v0.7.3 grid-size-probe.jpg.
                        // Bind it to the verified unit so changing IDs cannot silently reuse it.
                        nativeAds.Configure(tuning.HomeNativeAdUnitId,tuning.CompletedNativeAdUnitId,tuning.GameplayGridAdUnitId,
                            new NativeAdTemplateSize("adunit-2060c1fccf2ceecf",80,120));
                        if(!nativePresenter)nativePresenter=gameObject.AddComponent<NativeAdsPresenter>();
                        nativePresenter.Bind(presenter.Flow,nativeAds);
                    }
                    catch(Exception ex)
                    {
                        if(nativePresenter)nativePresenter.Bind(presenter.Flow,null);
                        Debug.LogWarning("PORTCARGO_NATIVE_ADS_UNAVAILABLE "+ex.GetType().Name);
                    }
                }
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                var launch=Environment.GetCommandLineArgs();
                for(var i=0;i<launch.Length-1;i++)
                {
                    if(launch[i]=="--portcargo-check-music")new GameObject("MusicProbe").AddComponent<DevelopmentChecks>().BeginMusicChecks(launch[i+1]);
                    if(launch[i]=="--portcargo-check-art")new GameObject("ArtProbe").AddComponent<DevelopmentChecks>().BeginArtChecks(launch[i+1]);
                    if(launch[i]=="--portcargo-observe-native")new GameObject("NativeObservation").AddComponent<DevelopmentChecks>().BeginNativeObservation(launch[i+1]);
                    if(launch[i]=="--portcargo-check-native-map")new GameObject("NativeMapProbe").AddComponent<DevelopmentChecks>().BeginNativeMap(launch[i+1]);
                    if(launch[i]=="--portcargo-check-technical")new GameObject("TechnicalProbe").AddComponent<DevelopmentChecks>().BeginTechnicalChecks(launch[i+1]);
                    if(launch[i]=="--portcargo-check-flow-edges")new GameObject("FlowEdgesProbe").AddComponent<DevelopmentChecks>().BeginFlowEdges(launch[i+1]);
                    if(launch[i]=="--portcargo-check-flow"||launch[i]=="--portcargo-check-flow-read")new GameObject("FlowProbe").AddComponent<DevelopmentChecks>().BeginFlowSmoke(launch[i+1],launch[i]=="--portcargo-check-flow-read");
                    if(launch[i]=="--portcargo-check-write"||launch[i]=="--portcargo-check-read")
                        new GameObject("PersistenceProbe").AddComponent<DevelopmentChecks>().BeginPersistence(launch[i+1],launch[i]=="--portcargo-check-write");
                    if(launch[i]=="--portcargo-check-sweep")new GameObject("SweepProbe").AddComponent<DevelopmentChecks>().BeginPlayerSweep(launch[i+1]);
                    if(launch[i]=="--portcargo-check-expansion"||launch[i]=="--portcargo-check-expansion-read")new GameObject("ExpansionProbe").AddComponent<DevelopmentChecks>().BeginExpansionSmoke(launch[i+1],launch[i]=="--portcargo-check-expansion-read");
                    if(launch[i]=="--portcargo-check-soak")new GameObject("SoakProbe").AddComponent<DevelopmentChecks>().BeginSoak(launch[i+1]);
                }
#endif
            }
            catch(Exception ex)
            {
                ShowBootError(ex);
            }
        }
        private void ShowBootError(Exception ex)
        {
                Debug.LogError("PORTCARGO_BOOT "+ex);
                if(presenter&&presenter.Flow)presenter.Flow.ShowBootError(RetryBoot);
                else if(dialog)dialog.Show("暂时无法进入","请重试",new[]{"重试","退出游戏"},choice=>{if(choice==0)RetryBoot();else WeChatPlatform.Exit();});
        }
        public static void ValidateSave(PlayerSave value,LevelDefinition[] levels)
        {
            ProgressValidator.Validate(value,levels);
        }
    }
}
