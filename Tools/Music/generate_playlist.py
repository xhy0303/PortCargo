"""Original deterministic harbor music; retains original masters and writes only Audio/MusicV041.
No external samples. See original_synthesis.py for shared timbre math and original score.
"""
from pathlib import Path
import wave,json,hashlib,math
import numpy as np
import original_synthesis as synth
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/PortCargo/Audio/MusicV041';OUT.mkdir(exist_ok=True)
SR=synth.SAMPLE_RATE
records=[]
def write(name,data,description):
 data=data-np.mean(data,axis=0)
 gain=min(10**(-24/20)/np.sqrt(np.mean(data**2)),10**(-7/20)/np.max(np.abs(data)))
 pcm=np.rint(data*gain*32767).astype('<i2')
 path=OUT/(name+'.wav')
 with wave.open(str(path),'wb') as w:
  w.setnchannels(2);w.setsampwidth(2);w.setframerate(SR);w.writeframes(pcm.tobytes())
 x=pcm.astype(float)/32768
 row=dict(name=name,description=description,seconds=len(x)/SR,peakDbfs=20*np.log10(abs(x).max()),rmsDbfs=20*np.log10(np.sqrt(np.mean(x*x))),clippedSamples=int(np.sum(abs(pcm.astype(int))>=32767)),seamStep=float(abs(x[0]-x[-1]).max()),internalStep=float(abs(np.diff(x,axis=0)).max()),sha256=hashlib.sha256(path.read_bytes()).hexdigest())
 assert row['clippedSamples']==0 and row['peakDbfs']<=-7
 assert row['seamStep']<row['internalStep']
 records.append(row)
with wave.open(str(ROOT/'Assets/PortCargo/Audio/FlatPixel/BGM/night_harbor_loop.wav'),'rb') as w:
 original=np.frombuffer(w.readframes(w.getnframes()),dtype='<i2').reshape(-1,2).astype(float)/32768
# Original tune plays twice before moving on; source remains byte-for-byte untouched.
write('night_harbor',np.tile(original,(2,1)),'Original 24-second score, two cycles, normalized; 48 seconds')
def compose(name,bpm,chords,melodies):
 beat=60/bpm;bar=beat*4
 data=np.zeros((round(16*bar*SR),2))
 for i in range(16):
  bass,chord=chords[i%8];base=i*bar
  # A quieter bridge for bars 9-12, then a melodic response in the final four bars.
  bridge=8<=i<12
  for at,note in [(0,bass),(2,bass+7 if i%2 else bass)]:
   synth.place(data,synth.tone(note,beat*1.9,'bass',.06,.3,.55),base+at*beat,.22,0,True)
  pattern=(0,2,1,3) if i%2==0 else (3,1,2,0)
  for step,j in enumerate(pattern):
   synth.place(data,synth.tone(chord[j],beat*1.5,'pulse',.04,.25,1.6),base+(step+.25)*beat,.075 if bridge else .1,(-1 if step%2 else 1)*.35,True)
  for at,note,gate in melodies[i%8]:
   pitch=note-12 if bridge else note
   synth.place(data,synth.tone(pitch,gate*beat+.25,'bell' if name=='lantern_current' else 'triangle',.025,.25,.9),base+at*beat,.12 if bridge else .17,.12 if i%2 else -.12,True)
  if i>=12:
   synth.place(data,synth.tone(chord[2]+12,.8*beat,'bell',.025,.15,2),base+3.5*beat,.045,.45,True)
 dry=data.copy()
 for delay,gain in [(.5,.12),(1,.055),(1.5,.02)]:data+=np.roll(dry[:,::-1],round(delay*beat*SR),axis=0)*gain
 write(name,data,f'Original 16-bar score, {bpm} BPM, bridge and response, no third-party samples')
compose('lantern_current',76,
 [(38,[57,60,64,65]),(43,[55,59,62,67]),(36,[55,60,64,67]),(45,[57,60,64,67]),(41,[57,60,64,69]),(40,[55,59,62,67]),(38,[57,60,62,65]),(43,[55,59,62,64])],
 [[(.5,77,1),(2,76,1.5)],[(0,74,1),(1.5,71,.75),(3,74,.75)],[(.5,76,1),(2.5,79,1)],[(0,76,1.5),(2,72,1.5)],[(.5,69,.75),(1.5,72,.75),(3,76,.75)],[(0,74,1.5),(2.5,71,1)],[(.5,69,1),(2,65,1.5)],[(0,67,1.5),(2.5,71,1)]])
compose('morning_tide',88,
 [(36,[55,60,64,67]),(43,[55,59,62,69]),(45,[57,60,64,67]),(41,[57,60,65,69]),(38,[57,60,62,65]),(43,[55,59,62,67]),(40,[55,59,64,67]),(45,[57,60,64,67])],
 [[(0,72,.75),(1,76,.75),(2.5,79,1)],[(.5,81,.75),(1.5,79,.75),(3,74,.75)],[(0,76,1.5),(2,72,.75),(3,69,.75)],[(.5,72,1),(2,77,1.5)],[(0,74,1),(1.5,77,.75),(3,76,.75)],[(.5,74,1),(2.5,71,1)],[(0,67,.75),(1,71,.75),(2.5,76,1)],[(0,72,1.5),(2.5,69,1)]])
(ROOT/'Docs/Evidence/v0.4.1/audio-masters.json').write_text(json.dumps(records,indent=2))
print(json.dumps(records,indent=2))
