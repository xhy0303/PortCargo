using UnityEngine;

namespace PortCargo.Runtime
{
    [CreateAssetMenu(menuName = "PortCargo/Game Tuning")]
    public sealed class GameTuning : ScriptableObject
    {
        [SerializeField] private int targetFps = 30;
        [SerializeField] private float boxMoveSeconds = .1f;
        [SerializeField] private float shipDepartSeconds = .22f;
        [SerializeField] private int solverNodes = 200000;
        [SerializeField] private float solverWallMilliseconds = 3000;
        [SerializeField] private bool adsEnabled;
        [SerializeField] private string rewardedHintAdUnitId="";
        [SerializeField] private string endlessSkipAdUnitId="";
        [SerializeField] private bool nativeAdsEnabled;
        [SerializeField] private string homeNativeAdUnitId="";
        [SerializeField] private string completedNativeAdUnitId="";
        [SerializeField] private string gameplayGridAdUnitId="";
        public int TargetFps => targetFps;
        public float BoxMoveSeconds => boxMoveSeconds;
        public float ShipDepartSeconds => shipDepartSeconds;
        public int SolverNodes => solverNodes;
        public float SolverWallMilliseconds => solverWallMilliseconds;
        // Only the WeChat player binds real providers. Other platforms keep the free flow.
        public bool AdsEnabled => adsEnabled;
        public string RewardedHintAdUnitId => rewardedHintAdUnitId;
        public string EndlessSkipAdUnitId => endlessSkipAdUnitId;
        public bool NativeAdsEnabled => adsEnabled && nativeAdsEnabled;
        public string HomeNativeAdUnitId => homeNativeAdUnitId;
        public string CompletedNativeAdUnitId => completedNativeAdUnitId;
        public string GameplayGridAdUnitId => gameplayGridAdUnitId;
        private void OnValidate()
        {
            targetFps = Mathf.Clamp(targetFps, 15, 60);
            boxMoveSeconds = Mathf.Clamp(boxMoveSeconds, .08f, .12f);
            solverNodes = Mathf.Clamp(solverNodes, 1, 200000);
            solverWallMilliseconds = Mathf.Clamp(solverWallMilliseconds, 1, 3000);
        }
    }
}
