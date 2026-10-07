#ifndef ROWDY_VEGETATION_DISPLACEMENT_INCLUDED
#define ROWDY_VEGETATION_DISPLACEMENT_INCLUDED

// Bends sprite vertices sideways: wind (global) + a per-plant bend set by InteractiveVegetation through a MaterialPropertyBlock.
// Works in world space, so it's correct whether or not sprites get batched.
// Expects these in the pass's UnityPerMaterial CBUFFER:
//   _VegBaseY, _VegHeight   world-space bottom and height of the sprite (set per renderer)
//   _VegBend                current bend, in fractions of the plant's height (+ = right)
//   _VegHanging             1 = anchored at the top (hanging vines)
//   _VegWindScale           how much this plant reacts to wind
//   _VegPhase               random offset so neighbors don't sway in sync

// Global wind, set by VegetationSystem
float _VegWindStrength;
float _VegWindSpeed;
float _VegWindFrequency;
float _VegPixelSnap; // world units per art pixel (1/64), 0 = off

float3 VegetationDisplace(float3 positionOS)
{
    float3 world = TransformObjectToWorld(positionOS);

    float h = saturate((world.y - _VegBaseY) / max(_VegHeight, 0.001));
    float hanging = step(0.5, _VegHanging);
    h = lerp(h, 1.0 - h, hanging);
    float weight = h * h; // stiff near the anchor, loose at the tip

    float phase = _Time.y * _VegWindSpeed + world.x * _VegWindFrequency + _VegPhase;
    float wind = (sin(phase) + 0.45 * sin(phase * 2.3 + 1.7)) * _VegWindStrength * _VegWindScale;

    float bend = _VegBend + wind;
    float offsetX = bend * weight * _VegHeight;
    // slight arc: tips dip while bending (rise for hanging vines)
    float offsetY = abs(bend) * weight * _VegHeight * 0.2 * lerp(-1.0, 1.0, hanging);

    if (_VegPixelSnap > 0.0)
    {
        offsetX = round(offsetX / _VegPixelSnap) * _VegPixelSnap;
        offsetY = round(offsetY / _VegPixelSnap) * _VegPixelSnap;
    }

    world.x += offsetX;
    world.y += offsetY;
    return TransformWorldToObject(world);
}

#endif
