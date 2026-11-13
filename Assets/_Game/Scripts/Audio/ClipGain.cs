using System;
using UnityEngine;

// Ajuste de volume de UM clipe (equalização): alguns arquivos vêm bem mais altos que outros.
// Preenchido pelo menu "Draw the Odds/Balance Audio Levels" (mede cada som), mas dá para mexer à mão no GameConfig.
[Serializable]
public class ClipGain
{
    public AudioClip clip;
    [Range(0f, 1f)] public float gain = 1f;  // multiplica o volume sempre que esse clipe toca
    public float measuredDb;                 // volume medido (dBFS, pico do RMS em janelas de 100 ms), só informativo
}
