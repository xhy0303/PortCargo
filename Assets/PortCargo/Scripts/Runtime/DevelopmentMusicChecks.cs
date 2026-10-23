#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PortCargo.Runtime
{
    public sealed partial class DevelopmentChecks
    {
        public void BeginMusicChecks(string prefix)
        {Initialize(prefix+".txt");StartCoroutine(Guarded(MusicChecks()));StartCoroutine(QuitAfterReport());}
        private IEnumerator MusicChecks()
        {
            while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
            var player=FindFirstObjectByType<HarborMusicPlayer>();
            Require(player,"playlist component missing");var source=player.GetComponent<AudioSource>();
            Require(!source.isPlaying,"music must wait for first interaction");
            yield return PressButton("设置");yield return new WaitForSecondsRealtime(.4f);
            Require(source.isPlaying&&source.clip.name=="night_harbor"&&source.volume>.6f,"menu interaction starts first track at new volume");
            var clip=source.clip;var cursor=source.time;
            yield return PressButton("音乐：开");yield return new WaitForSecondsRealtime(.3f);
            Require(!source.isPlaying&&source.volume==0,"music off must silence and pause");
            var mutedAt=source.time;yield return new WaitForSecondsRealtime(.3f);
            Require(Mathf.Abs(source.time-mutedAt)<.15f,"muted music cursor advances");
            yield return PressButton("音效：开");yield return PressButton("音乐：关");yield return new WaitForSecondsRealtime(.4f);
            Require(source.isPlaying&&source.clip==clip&&source.time>=cursor,"music resumes without restart, even with SFX off");
            yield return PressButton("音效：关");yield return PressButton("返回");
            Require(source.clip==clip&&source.isPlaying,"home navigation interrupts music");
            cursor=source.time;presenter.HandleHidden();yield return new WaitForSecondsRealtime(.4f);
            Require(!source.isPlaying,"application hidden does not pause music");
            presenter.HandleShown();yield return new WaitForSecondsRealtime(.4f);
            Require(source.isPlaying&&source.clip==clip&&source.time>=cursor,"application shown restarts music");
            player.SendMessage("OnApplicationFocus",false);yield return new WaitForSecondsRealtime(.2f);
            Require(!source.isPlaying,"focus loss music pause");
            // A show callback cannot override a simultaneous loss of focus.
            presenter.HandleShown();yield return new WaitForSecondsRealtime(.2f);Require(!source.isPlaying,"show bypasses focus suspension");
            player.SendMessage("OnApplicationFocus",true);yield return new WaitForSecondsRealtime(.3f);
            Require(source.isPlaying,"focus regain resume");
            report.Add("MUSIC PASS first interaction, independent music/SFX toggles, retained cursor, home/settings, hidden/shown and simulated focus callback ordering");
            // Observe real playback, rather than seeking or shortening the source clips.
            var names=new[]{"night_harbor","lantern_current","morning_tide"};
            var peaks=new Dictionary<string,float>();var counts=new Dictionary<string,int>();
            var samples=new float[1024];var changes=0;var prior=source.clip;var gap=0f;var largestGap=0f;
            var expectedIndex=0;var elapsed=0f;var nextReport=30f;
            var originalSources=FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length;
            while(changes<6&&elapsed<400)
            {
                yield return null;elapsed+=Time.unscaledDeltaTime;
                if(!source.isPlaying){gap+=Time.unscaledDeltaTime;largestGap=Mathf.Max(gap,largestGap);Require(gap<.5f,"playlist stalled");continue;}
                gap=0;
                if(source.clip!=prior)
                {
                    changes++;expectedIndex=(expectedIndex+1)%3;
                    Require(source.clip.name==names[expectedIndex],"playlist skips or repeats track");
                    prior=source.clip;report.Add("MUSIC transition="+changes+" track="+prior.name+" elapsed="+elapsed.ToString("F2"));
                }
                if(!peaks.ContainsKey(source.clip.name)){peaks[source.clip.name]=0;counts[source.clip.name]=0;}
                source.GetOutputData(samples,0);var peak=0f;foreach(var sample in samples)peak=Mathf.Max(peak,Mathf.Abs(sample));
                peaks[source.clip.name]=Mathf.Max(peaks[source.clip.name],peak);
                if(peak>.0001f)counts[source.clip.name]++;
                Require(peak<.99f,"music output clips");
                Require(FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length==originalSources,"playlist creates audio sources");
                if(elapsed>=nextReport){System.IO.File.WriteAllLines(output,report);nextReport+=30;}
            }
            Require(changes==6,"two complete playlist cycles did not finish");
            foreach(var name in names){Require(peaks.ContainsKey(name)&&peaks[name]>.005f&&counts[name]>100,"track output silent: "+name);report.Add("MUSIC OUTPUT "+name+" peak="+peaks[name]+" audibleFrames="+counts[name]);}
            report.Add("MUSIC PASS two natural playlist cycles, six transitions, elapsed="+elapsed+"s maxGap="+largestGap+" sources="+originalSources+"; output samples measured, not a subjective listening verdict");
        }
    }
}
#endif
