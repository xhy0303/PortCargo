using System.Collections.Generic;
using PortCargo.Core;
using UnityEngine;

namespace PortCargo.Runtime
{
    /// <summary>Fixed-size effects pool. Stable state is already committed before an effect starts.</summary>
    public sealed class FeedbackPlayer : MonoBehaviour
    {
        [SerializeField] private CargoGraphic[] ghosts;
        [SerializeField] private AudioSource[] voices;
        [SerializeField] private AudioClip pickClip, dockClip, completeClip, rejectClip, menuClip;
        [SerializeField] private AudioSource musicSource;
        private HarborMusicPlayer Music => musicSource?musicSource.GetComponent<HarborMusicPlayer>():null;
        [SerializeField] private RectTransform[] departingShips;
        [SerializeField] private CargoGraphic[] departingCargo;
        [SerializeField] private TMPro.TMP_Text[] departingCounts;
        [SerializeField] private CanvasGroup[] departingAlpha;
        [SerializeField] private UnityEngine.UI.Image[] confetti;
        [SerializeField] private RectTransform dock;
        private readonly float[] shipBegan=new float[3];
        private float shipSeconds=.22f, celebrateUntil;
        private int nextShip;
        private bool musicEnabled=true;
        private readonly Vector3[] starts=new Vector3[6], ends=new Vector3[6];
        private readonly float[] began=new float[6], duration=new float[6];
        private int nextGhost, nextVoice;
        private bool sound=true, reduced;
        private float lastReject=-1;
        public void Configure(bool enableSound,bool reduce,bool enableMusic)
        {
            sound=enableSound;reduced=reduce;musicEnabled=enableMusic;
            if(!sound)foreach(var voice in voices)if(voice)voice.Stop();
            if(Music)Music.SetEnabled(musicEnabled);
        }
        public void Play(LevelDefinition level,IReadOnlyList<DomainEvent> events,Vector3 origin,Vector3 ship,BoardView board,float moveSeconds,float departSeconds)
        {
            if(Music)Music.Interact();
            shipSeconds=departSeconds;var departures=0;var complete=false;
            foreach(var e in events)
            {
                if(e.Kind==EventKind.ShipDeparted){departures++;Depart(level,e.ShipId);}
                if(e.Kind==EventKind.BoxRemoved&&board.IsStackEmpty(e.StackIndex))board.PulseCleared(e.StackIndex);
                if(e.Kind==EventKind.LevelCompleted)complete=true;
                if(e.Kind==EventKind.BoxToShip||e.Kind==EventKind.BoxToBuffer||e.Kind==EventKind.BufferedBoxToShip)
                {
                    level.TryGetBox(e.BoxId,out var box);
                    var target=e.Kind==EventKind.BoxToBuffer?board.BufferPosition(e.Amount-1):ship;
                    var source=e.Kind==EventKind.BufferedBoxToShip?board.BufferPosition(0):origin;
                    Flight(box.Type,source,target,moveSeconds);
                }
            }
            if(complete&&!reduced){celebrateUntil=Time.unscaledTime+.45f;foreach(var bit in confetti)bit.gameObject.SetActive(true);}
            PlaySound(complete?completeClip:departures>0?dockClip:pickClip,1+Mathf.Min(2,departures)*.1f);
        }
        private void Depart(LevelDefinition level,string id)
        {
            if(departingShips==null||departingShips.Length==0)return;
            var i=nextShip++%departingShips.Length;
            foreach(var ship in level.Ships)if(ship.Id==id){departingCargo[i].Set(ship.Type);departingCounts[i].text=ship.Capacity+" / "+ship.Capacity;break;}
            departingShips[i].gameObject.SetActive(true);departingShips[i].position=dock.position;
            departingAlpha[i].alpha=1;shipBegan[i]=Time.unscaledTime;
        }
        private void Flight(string type,Vector3 from,Vector3 to,float seconds)
        {
            var i=nextGhost++%ghosts.Length;var ghost=ghosts[i];
            ghost.Set(type);ghost.gameObject.SetActive(true);ghost.transform.position=from;
            starts[i]=from;ends[i]=to;began[i]=Time.unscaledTime;duration[i]=seconds;
        }
        public void Menu(){if(Music)Music.Interact();PlaySound(menuClip,1);}
        public void Reject(){if(Time.unscaledTime-lastReject<.15f)return;lastReject=Time.unscaledTime;PlaySound(rejectClip,1);}
        private void PlaySound(AudioClip clip,float pitch)
        {
            if(!sound||!clip)return;var voice=voices[nextVoice++%voices.Length];voice.Stop();voice.clip=clip;voice.pitch=pitch;voice.Play();
        }
        public void Cancel(bool stopSounds=false)
        {
            foreach(var ghost in ghosts)if(ghost)ghost.gameObject.SetActive(false);
            if(stopSounds)foreach(var voice in voices)if(voice)voice.Stop();
            if(departingShips!=null)foreach(var ship in departingShips)if(ship)ship.gameObject.SetActive(false);
            if(confetti!=null)foreach(var bit in confetti)if(bit)bit.gameObject.SetActive(false);
            celebrateUntil=0;
        }
        public void PauseAudio(){Cancel(true);if(Music)Music.SetHidden(true);}
        public void ResumeAudio(){if(Music)Music.SetHidden(false);}
        private void Update()
        {
            for(var i=0;departingShips!=null&&i<departingShips.Length;i++)
            {
                if(!departingShips[i].gameObject.activeSelf)continue;
                var t=Mathf.Clamp01((Time.unscaledTime-shipBegan[i])/shipSeconds);
                departingShips[i].position=dock.position+Vector3.right*(240*t*dock.lossyScale.x);
                departingAlpha[i].alpha=1-t;if(t>=1)departingShips[i].gameObject.SetActive(false);
            }
            for(var i=0;confetti!=null&&i<confetti.Length;i++)
            {
                if(!confetti[i].gameObject.activeSelf)continue;
                var t=1-(celebrateUntil-Time.unscaledTime)/.45f;
                if(t>=1||reduced){confetti[i].gameObject.SetActive(false);continue;}
                var angle=(i*137.5f)*Mathf.Deg2Rad;
                confetti[i].rectTransform.anchoredPosition=new Vector2(Mathf.Cos(angle)*180*t,80+Mathf.Sin(angle)*130*t-140*t*t);
                var c=confetti[i].color;c.a=1-t;confetti[i].color=c;
            }
            for(var i=0;i<ghosts.Length;i++)
            {
                if(!ghosts[i].gameObject.activeSelf)continue;
                var t=(Time.unscaledTime-began[i])/duration[i];
                if(t>=1){ghosts[i].gameObject.SetActive(false);continue;}
                var arc=reduced?0:Mathf.Sin(t*Mathf.PI)*24;
                ghosts[i].transform.position=Vector3.Lerp(starts[i],ends[i],t)+Vector3.up*arc;
            }
        }
    }
}
