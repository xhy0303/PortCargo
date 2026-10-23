#!/usr/bin/env python3
"""Port Cargo / Flat Pixel Night Harbor: deterministic original audio masters.

Requires Python 3.10+, numpy and fontTools. No network, sample library or DAW.
Run from any directory: python3 generate_audio_and_font_manifest.py
Optional: --project /path/to/PortCargo for a fresh read-only Runtime font audit.
Optional: --audio-only needs numpy only. Writes only this delivery package.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import re
import shutil
import wave
from pathlib import Path

import numpy as np

ROOT = Path(__file__).resolve().parents[2]
SAMPLE_RATE = 44100
SEED = 20261016  # Reserved for future variation; this score has no randomness.
BPM = 80
BEAT = 60.0 / BPM
BAR = BEAT * 4
BARS = 8
LOOP_SECONDS = BAR * BARS
SFX_PEAK_DBFS = {'pick': -8.0, 'depart': -9.0, 'complete': -8.0,
                 'reject': -12.0, 'menu': -11.0}
BGM_PEAK_DBFS = -17.0
TUNING_HZ = 440.0


def midi_frequency(note: float) -> float:
    return TUNING_HZ * 2.0 ** ((note - 69.0) / 12.0)


def envelope(count: int, attack: float, release: float, decay: float = 0.0) -> np.ndarray:
    """Sine-squared ramps begin/end at zero, with a gentle exponential body."""
    value = np.ones(count, dtype=np.float64)
    attack_n = min(count // 2, max(2, round(attack * SAMPLE_RATE)))
    release_n = min(count - attack_n, max(2, round(release * SAMPLE_RATE)))
    value[:attack_n] *= np.sin(np.linspace(0.0, np.pi / 2.0, attack_n)) ** 2
    value[-release_n:] *= np.cos(np.linspace(0.0, np.pi / 2.0, release_n)) ** 2
    if decay:
        value *= np.exp(-decay * np.arange(count) / SAMPLE_RATE)
    value[0] = value[-1] = 0.0
    return value


def tone(note: float, seconds: float, voice: str = 'triangle',
         attack: float = 0.009, release: float = 0.07, decay: float = 1.8,
         bend_semitones: float = 0.0) -> np.ndarray:
    """Band-limited, gentle chip timbres; no hard square-wave discontinuity."""
    count = round(seconds * SAMPLE_RATE)
    t = np.arange(count, dtype=np.float64) / SAMPLE_RATE
    freq = midi_frequency(note) * 2.0 ** ((bend_semitones * t / seconds) / 12.0)
    phase = 2 * np.pi * np.cumsum(freq) / SAMPLE_RATE
    phase -= phase[0]
    value = np.zeros(count, dtype=np.float64)
    if voice == 'triangle':
        for harmonic in (1, 3, 5, 7):
            if harmonic * np.max(freq) < SAMPLE_RATE * 0.45:
                value += (-1) ** ((harmonic - 1) // 2) * np.sin(harmonic * phase) / harmonic ** 2
    elif voice == 'pulse':
        # A soft 25% pulse impression made from only five low harmonics.
        for harmonic in range(1, 6):
            if harmonic * np.max(freq) < SAMPLE_RATE * 0.45:
                value += (np.sin(np.pi * harmonic * 0.25) / harmonic ** 1.8) * np.sin(harmonic * phase)
    elif voice == 'bell':
        value = np.sin(phase) + 0.16 * np.sin(2 * phase) * np.exp(-8 * t)
    elif voice == 'bass':
        value = np.sin(phase) + 0.06 * np.sin(2 * phase)
    else:
        raise ValueError(voice)
    return value * envelope(count, attack, release, decay)


def place(track: np.ndarray, signal: np.ndarray, at_seconds: float,
          gain: float = 1.0, pan: float = 0.0, wrap: bool = False) -> None:
    start = round(at_seconds * SAMPLE_RATE)
    if track.ndim == 2:
        # Equal-power panning, pan in [-1, 1].
        theta = (pan + 1.0) * np.pi / 4.0
        signal = signal[:, None] * np.array([np.cos(theta), np.sin(theta)])
    signal = signal * gain
    if wrap:
        indices = (np.arange(len(signal)) + start) % len(track)
        np.add.at(track, indices, signal)
    else:
        assert 0 <= start and start + len(signal) <= len(track)
        track[start:start + len(signal)] += signal


def peak_normalize(data: np.ndarray, peak_dbfs: float) -> np.ndarray:
    return data * (10.0 ** (peak_dbfs / 20.0)) / np.max(np.abs(data))


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_wav(relative: str, data: np.ndarray, peak_dbfs: float,
              purpose: str, loop: bool = False) -> dict:
    destination = ROOT / relative
    destination.parent.mkdir(parents=True, exist_ok=True)
    data = peak_normalize(data, peak_dbfs)
    if not loop:
        data[0] = 0.0
        data[-1] = 0.0
    pcm = np.rint(data * 32767).astype('<i2')
    channels = 1 if pcm.ndim == 1 else pcm.shape[1]
    with wave.open(str(destination), 'wb') as stream:
        stream.setnchannels(channels)
        stream.setsampwidth(2)
        stream.setframerate(SAMPLE_RATE)
        stream.writeframes(pcm.tobytes(order='C'))
    measured = pcm.astype(np.float64) / 32768.0
    peak = float(np.max(np.abs(measured)))
    rms = float(np.sqrt(np.mean(measured ** 2)))
    differences = np.diff(measured, axis=0)
    seam = np.atleast_1d(measured[0] - measured[-1])
    result = {
        'path': relative, 'purpose': purpose, 'format': 'WAV PCM16 signed little-endian',
        'sample_rate_hz': SAMPLE_RATE, 'channels': channels, 'bit_depth': 16,
        'sample_frames': len(pcm), 'duration_seconds': len(pcm) / SAMPLE_RATE,
        'peak_dbfs': round(20 * math.log10(peak), 4),
        'rms_dbfs': round(20 * math.log10(rms), 4),
        'clipped_sample_count': int(np.sum(np.abs(pcm.astype(np.int32)) >= 32767)),
        'first_sample_pcm16': np.atleast_1d(pcm[0]).astype(int).tolist(),
        'last_sample_pcm16': np.atleast_1d(pcm[-1]).astype(int).tolist(),
        'first_minus_last_pcm16': np.atleast_1d(pcm[0].astype(np.int32) - pcm[-1].astype(np.int32)).tolist(),
        'seam_step_linear_fs': seam.tolist(),
        'max_internal_step_linear_fs': float(np.max(np.abs(differences))),
        'loop': loop, 'loop_start_sample': 0 if loop else None,
        'loop_end_exclusive_sample': len(pcm) if loop else None,
        'bytes': destination.stat().st_size, 'sha256': sha256(destination),
        'source': '02_可编辑源文件/Audio/generate_audio_and_font_manifest.py',
    }
    assert result['peak_dbfs'] <= -3.0
    assert result['clipped_sample_count'] == 0
    if loop:
        # A seam is a normal adjacent-sample step, not required to repeat a frame.
        assert np.max(np.abs(seam)) < np.max(np.abs(differences))
    return result


def render_effects() -> list[dict]:
    effects = []
    score = {
        # at seconds, MIDI note, duration, voice, relative gain, bend in semitones
        'pick': (0.18, [(0.0, 76, 0.105, 'bell', 1.0, 0.0),
                        (0.052, 83, 0.10, 'triangle', 0.42, 0.0)]),
        'depart': (0.82, [(0.0, 55, 0.54, 'bass', 0.35, 0.0),
                         (0.015, 67, 0.26, 'triangle', 0.7, 0.0),
                         (0.19, 71, 0.26, 'triangle', 0.60, 0.0),
                         (0.375, 74, 0.405, 'pulse', 0.52, 0.0)]),
        'complete': (1.65, [(0.0, 72, 0.28, 'triangle', 0.85, 0.0),
                           (0.22, 76, 0.28, 'triangle', 0.80, 0.0),
                           (0.44, 79, 0.28, 'triangle', 0.74, 0.0),
                           (0.68, 84, 0.78, 'bell', 0.70, 0.0),
                           (0.68, 72, 0.82, 'triangle', 0.23, 0.0),
                           (0.68, 76, 0.82, 'triangle', 0.20, 0.0)]),
        'reject': (0.28, [(0.0, 60, 0.22, 'triangle', 0.85, -1.5),
                         (0.06, 55, 0.19, 'bass', 0.32, 0.0)]),
        'menu': (0.13, [(0.0, 79, 0.105, 'bell', 0.72, 0.0)]),
    }
    purposes = {'pick': '取箱：短促清脆，轻音两级上行',
                'depart': '出港：柔和三音上行，低音作短支撑',
                'complete': '通关：明亮但克制的原创短旋律',
                'reject': '无效操作：低音量柔和下行，不使用蜂鸣惩罚感',
                'menu': '菜单确认：可选短提示音'}
    for name, (seconds, notes) in score.items():
        data = np.zeros(round(seconds * SAMPLE_RATE))
        for start, midi, duration, voice, gain, bend in notes:
            place(data, tone(midi, duration, voice=voice,
                             attack=0.007 if name != 'reject' else 0.014,
                             release=min(0.10, duration * 0.45),
                             decay=2.2, bend_semitones=bend), start, gain)
        effects.append(write_wav(f'01_可交付/Audio/SFX/{name}.wav', data,
                                 SFX_PEAK_DBFS[name], purposes[name]))
    return effects


def render_bgm() -> dict:
    """24 s / eight bars; events and delay tails accumulate on a periodic buffer."""
    data = np.zeros((round(LOOP_SECONDS * SAMPLE_RATE), 2))
    # Eight original bars: Fmaj7 / G6 / Em7 / Am7 repeated with melodic response.
    harmony = [(41, [57, 60, 64, 69]), (43, [55, 59, 62, 64]),
               (40, [55, 59, 62, 67]), (45, [57, 60, 64, 67]),
               (41, [57, 60, 64, 69]), (43, [55, 59, 62, 64]),
               (40, [55, 59, 62, 67]), (45, [57, 60, 64, 67])]
    # Each event: beat offset, MIDI pitch, gate duration in beats.
    melody = [
        [(0.5, 76, 1.0), (2.0, 79, 1.4)],
        [(0.5, 74, 1.1), (2.5, 71, 1.0)],
        [(0.5, 74, 1.4), (2.5, 76, 1.0)],
        [(0.0, 72, 2.0), (2.5, 69, 1.2)],
        [(0.5, 72, 1.0), (2.0, 76, 1.3)],
        [(0.5, 74, 1.1), (2.5, 79, 1.0)],
        [(0.5, 76, 1.2), (2.5, 74, 1.0)],
        [(0.0, 72, 1.8), (2.5, 69, 1.4)],
    ]
    for bar, (bass, chord) in enumerate(harmony):
        base = bar * BAR
        for beat in (0.0, 2.0):
            place(data, tone(bass, 1.65, 'bass', 0.055, 0.40, 0.6),
                  base + beat * BEAT, 0.24, 0.0, True)
        # Quiet broken chords: no noise percussion and no full-volume square wave.
        for step, note_index in enumerate((0, 2, 1, 3)):
            place(data, tone(chord[note_index], 1.20, 'pulse', 0.045, 0.35, 1.8),
                  base + step * BEAT, 0.105, -0.30 if step % 2 == 0 else 0.30, True)
        for beat, pitch, gate in melody[bar]:
            place(data, tone(pitch, gate * BEAT + 0.26, 'triangle', 0.025, 0.26, 0.85),
                  base + beat * BEAT, 0.17, -0.08 if bar % 2 == 0 else 0.08, True)
    # Circular delays: the beginning already contains the previous loop's tails.
    dry = data.copy()
    for delay_beats, gain, swap in ((0.5, 0.10, True), (1.0, 0.055, False), (1.5, 0.025, True)):
        wet = np.roll(dry[:, ::-1] if swap else dry,
                      round(delay_beats * BEAT * SAMPLE_RATE), axis=0)
        data += wet * gain
    data -= np.mean(data, axis=0)
    result = write_wav('01_可交付/Audio/BGM/night_harbor_loop.wav', data, BGM_PEAK_DBFS,
                       '像素夜港：80 BPM、4/4、8 小节、原创宁静循环', True)
    result.update({'bpm': BPM, 'meter': '4/4', 'bars': BARS,
                   'loop_construction': 'periodic event accumulation + circular stereo delays',
                   'fade_in_out_baked': False})
    return result


def csharp_string_literals(text: str) -> list[str]:
    """Read C# quoted strings, ignoring //, /* */ and character literals.

    Supports normal/verbatim/interpolated strings used in this project. Reports
    CJK in interpolation contents conservatively too; no C# execution involved.
    """
    tokens = re.compile(r'//[^\n]*|/\*[\s\S]*?\*/|\'(?:\\.|[^\'\\])*\'|(?:\$@|@\$|@)"(?:""|[^"])*"|\$?"(?:\\.|[^"\\])*"')
    strings = []
    for match in tokens.finditer(text):
        token = match.group()
        if token.startswith(('//', '/*', "'")):
            continue
        content = token[token.index('"') + 1:-1]
        if '@' in token[:token.index('"')]:
            content = content.replace('""', '"')
        else:
            content = re.sub(r'\\u([0-9a-fA-F]{4})|\\U([0-9a-fA-F]{8})',
                             lambda m: chr(int(m.group(1) or m.group(2), 16)), content)
        strings.append(content)
    return strings


def is_cjk(char: str) -> bool:
    return ('\u3400' <= char <= '\u4dbf' or '\u4e00' <= char <= '\u9fff'
            or '\uf900' <= char <= '\ufaff')


def copy_and_audit_fonts(project: Path | None = None) -> dict:
    from fontTools.ttLib import TTFont

    output = ROOT / '01_可交付/Fonts'
    output.mkdir(parents=True, exist_ok=True)
    previous_path = output / 'font-coverage.json'
    previous = json.loads(previous_path.read_text(encoding='utf-8')) if previous_path.exists() else {}
    prior_hashes = {Path(item['path']).name: item for item in previous.get('copy_hashes', [])}
    font_source = project / 'Assets/PortCargo/Art/Fonts' if project else None
    runtime_source = project / 'Assets/PortCargo/Scripts/Runtime' if project else None
    copied = []
    for name in ('PortCargoUI.ttf', 'OFL.txt', 'ui-characters.txt'):
        source = font_source / name if font_source else None
        destination = output / name
        # A standalone package already contains these files. An explicit project
        # may fill missing files but never silently replaces the delivered font.
        if not destination.exists():
            if source is not None and source.exists():
                shutil.copy2(source, destination)
            else:
                raise FileNotFoundError(f'Missing bundled font file: {destination}. Restore it from the delivery package, or use --audio-only.')
        source_available = source is not None and source.exists()
        original = prior_hashes.get(name, {})
        source_hash = sha256(source) if source_available else original.get('source_sha256')
        copied.append({'path': str(destination.relative_to(ROOT)),
                       'source': str(source) if source_available else original.get('source', 'bundled delivery file'),
                       'bytes': destination.stat().st_size, 'sha256': sha256(destination),
                       'source_sha256': source_hash,
                       'source_verified_this_run': source_available,
                       'matches_source_hash': sha256(destination) == source_hash if source_hash else None})
    font = TTFont(output / 'PortCargoUI.ttf')
    cmap = font.getBestCmap()
    runtime_chars = set()
    runtime_files = []
    missing_by_file = []
    if project is None:
        scan_status = 'skipped_no_project'
    elif runtime_source is None or not runtime_source.is_dir():
        scan_status = 'skipped_missing_runtime_directory'
    else:
        scan_status = 'completed'
    for path in sorted(runtime_source.rglob('*.cs')) if scan_status == 'completed' else []:
        literal_chars = set(''.join(csharp_string_literals(path.read_text(encoding='utf-8'))))
        chinese = {char for char in literal_chars if is_cjk(char)}
        runtime_chars.update(chinese)
        missing = ''.join(sorted(char for char in chinese if ord(char) not in cmap))
        runtime_files.append({'path': str(path.relative_to(project)), 'sha256': sha256(path)})
        if missing:
            missing_by_file.append({'path': str(path.relative_to(project)), 'missing_chinese': missing})
    all_cmap_characters = ''.join(chr(cp) for cp in sorted(cmap))
    (output / 'cmap-actual-characters.txt').write_text(all_cmap_characters + '\n', encoding='utf-8')
    missing = ''.join(sorted(char for char in runtime_chars if ord(char) not in cmap))
    report = {
        'font_family': 'PortCargoUI', 'font_style': 'Regular',
        'provenance': 'Existing renamed Noto Sans SC UI subset from the read-only project',
        'license': 'SIL Open Font License 1.1; full original OFL.txt copied unchanged',
        'cmap_codepoint_count': len(cmap),
        'cmap_chinese_codepoint_count': sum(is_cjk(chr(cp)) for cp in cmap),
        'glyph_count': len(font.getGlyphOrder()),
        'complete_chinese_font': False,
        'copy_hashes': copied,
        'bundled_font_check_status': 'completed',
        'runtime_scan_status': scan_status,
        'runtime_scan_root': str(runtime_source) if runtime_source else None,
        'runtime_csharp_file_count': len(runtime_files) if scan_status == 'completed' else None,
        'runtime_files_scanned': runtime_files,
        'runtime_string_chinese_count': len(runtime_chars) if scan_status == 'completed' else None,
        'runtime_string_chinese': ''.join(sorted(runtime_chars)) if scan_status == 'completed' else None,
        'missing_runtime_chinese_count': len(missing) if scan_status == 'completed' else None,
        'missing_runtime_chinese': missing if scan_status == 'completed' else None,
        'missing_by_file': missing_by_file,
        'scan_limit': 'Runtime C# string literals only; excludes dynamic external data, future text, Editor strings and TMP atlas contents',
        'unity_tmp_rebuilt': False,
    }
    # Preserve the delivered source audit as explicitly historical evidence when
    # rebuilding without the Unity project; never present it as a fresh scan.
    if scan_status != 'completed':
        if previous.get('runtime_scan_status', 'completed') == 'completed' and previous.get('runtime_csharp_file_count'):
            report['reference_runtime_scan'] = {
                key: value for key, value in previous.items()
                if key.startswith(('runtime_', 'missing_'))
            }
            report['reference_runtime_scan']['evidence_status'] = 'previous_report_not_rechecked'
        elif 'reference_runtime_scan' in previous:
            report['reference_runtime_scan'] = previous['reference_runtime_scan']
    (output / 'font-coverage.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    return report


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', type=Path, help='Optional read-only Unity project root; omitted by default.')
    parser.add_argument('--audio-only', action='store_true', help='Rebuild audio without fontTools or font inputs.')
    args = parser.parse_args()
    project = args.project.expanduser().resolve() if args.project else None
    audio = render_effects() + [render_bgm()]
    fonts = ({'bundled_font_check_status': 'skipped_audio_only', 'runtime_scan_status': 'skipped_audio_only'}
             if args.audio_only else copy_and_audit_fonts(project))
    manifest = {'schema_version': 2, 'audio_original_composition': True,
                'audio_third_party_samples': False, 'deterministic_seed': SEED,
                'audio': audio, 'fonts': fonts,
                'validation_scope': 'PCM format, duration, peak, RMS, clipping, loop seam, font cmap and Runtime text coverage; no Unity import, runtime playback or target-device listening performed'}
    output = ROOT / '04_预览与验收/audio-font-manifest.json'
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    for audio in manifest['audio']:
        print(f"{audio['path']}: {audio['duration_seconds']:.3f}s, peak {audio['peak_dbfs']:.2f} dBFS, RMS {audio['rms_dbfs']:.2f} dBFS, seam {audio['first_minus_last_pcm16']}")
    if args.audio_only:
        print('SKIP: font checks (--audio-only). Audio masters rebuilt successfully.')
    else:
        print(f"Bundled font cmap: {fonts['cmap_codepoint_count']}; Runtime scan: {fonts['runtime_scan_status']}")
        if fonts['runtime_scan_status'] == 'completed':
            print(f"Runtime Chinese: {fonts['runtime_string_chinese_count']}; missing: {fonts['missing_runtime_chinese']!r}")
        else:
            print('SKIP: no usable Unity Runtime source supplied; bundled font/hash checks completed. Prior source evidence, if present, is reference only.')


if __name__ == '__main__':
    main()
