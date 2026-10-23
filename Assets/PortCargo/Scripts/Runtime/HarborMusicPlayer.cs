using UnityEngine;

namespace PortCargo.Runtime
{
    /// <summary>One reusable source, ordered playlist, and resumable music independent of board state.</summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class HarborMusicPlayer : MonoBehaviour
    {
        [SerializeField] private AudioClip[] tracks;
        [SerializeField, Range(0,1)] private float volume=.65f;
        private AudioSource source;
        private bool enabledByUser=true, interacted, hidden, unfocused, paused;
        private int index;
        private bool awaitingPlayback;
        private float playRequestedAt;
        private const float FadeSeconds=.12f;
        private void Awake()
        {
            source=GetComponent<AudioSource>();source.Stop();source.clip=null;source.loop=false;
            source.playOnAwake=false;source.volume=0;source.spatialBlend=0;
        }
        public void SetEnabled(bool value){enabledByUser=value;}
        public void Interact(){interacted=true;}
        public void SetHidden(bool value){hidden=value;if(value)Suspend();}
        private void OnApplicationFocus(bool focused){unfocused=!focused;if(!focused)Suspend();}
        private void Suspend()
        {
            if(!source)return;
            if(source.isPlaying){source.Pause();paused=true;}
            if(source.volume!=0)source.volume=0;
        }
        private void Update()
        {
            if(!source||tracks==null||tracks.Length==0)return;
            if(hidden||unfocused||!interacted){Suspend();return;}
            if(!enabledByUser)
            {
                SetVolume(Mathf.MoveTowards(source.volume,0,volume*Time.unscaledDeltaTime/FadeSeconds));
                if(source.volume<=0)Suspend();
                return;
            }
            if(paused){source.UnPause();paused=false;awaitingPlayback=true;playRequestedAt=Time.realtimeSinceStartup;}
            if(awaitingPlayback)
            {
                if(source.isPlaying)awaitingPlayback=false;
                else if(Time.realtimeSinceStartup-playRequestedAt<10f)return;
                else
                {
                    // The WeChat native audio backend starts asynchronously. Retry the same
                    // track after timeout; never treat pending playback as a natural ending.
                    Debug.LogWarning("PORTCARGO_MUSIC_START_RETRY "+source.clip?.name);
                    source.Play();playRequestedAt=Time.realtimeSinceStartup;return;
                }
            }
            if(!source.isPlaying)
            {
                // Natural end advances exactly once. Muting and app suspension retain the sample cursor.
                if(source.clip)index=(index+1)%tracks.Length;
                source.clip=tracks[index];source.volume=0;
                if(!source.clip)return;
                source.Play();awaitingPlayback=true;playRequestedAt=Time.realtimeSinceStartup;
            }
            var remaining=Mathf.Max(0,source.clip.length-source.time);
            var target=volume*Mathf.Clamp01(remaining/FadeSeconds);
            SetVolume(Mathf.MoveTowards(source.volume,target,volume*Time.unscaledDeltaTime/FadeSeconds));
        }
        private void SetVolume(float value)
        {
            // Native Web audio setters cross into JS. A steady track needs no repeated writes.
            if(source.volume!=value)source.volume=value;
        }
        private void OnDisable(){Suspend();}
    }
}
