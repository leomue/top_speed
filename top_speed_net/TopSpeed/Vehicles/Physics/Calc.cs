using System;
using TopSpeed.Common;
using TopSpeed.Data;

namespace TopSpeed.Vehicles
{
    internal partial class Car
    {
        private void GuardDynamicInputs()
        {
            if (!IsFinite(_speed))
                _speed = 0f;
            if (!IsFinite(_positionX))
                _positionX = 0f;
            if (!IsFinite(_positionY))
                _positionY = 0f;
            if (_positionY < 0f)
                _positionY = 0f;
        }

        private void ApplySurfaceModifiers()
        {
            _currentSurfaceTractionFactor = _surfaceTractionFactor;
            _currentDeceleration = _deceleration;
            _speedDiff = 0f;

            switch (_surface)
            {
                case TrackSurface.Gravel:
                    _currentSurfaceTractionFactor = (_currentSurfaceTractionFactor * 2f) / 3f;
                    _currentDeceleration = (_currentDeceleration * 2f) / 3f;
                    break;
                case TrackSurface.Water:
                    _currentSurfaceTractionFactor = (_currentSurfaceTractionFactor * 3f) / 5f;
                    _currentDeceleration = (_currentDeceleration * 3f) / 5f;
                    break;
                case TrackSurface.Sand:
                    _currentSurfaceTractionFactor *= 0.5f;
                    _currentDeceleration = (_currentDeceleration * 3f) / 2f;
                    break;
                case TrackSurface.Snow:
                    _currentDeceleration *= 0.5f;
                    break;
            }
        }

        private int ResolveThrust()
        {
            if (_currentThrottle == 0)
                return _currentBrake;
            if (_currentBrake == 0)
                return _currentThrottle;
            return -_currentBrake > _currentThrottle ? _currentBrake : _currentThrottle;
        }

        private void ApplyThrottleDrive(
            float elapsed,
            float speedMpsCurrent,
            float throttle,
            bool inReverse,
            bool reverseBlockedAtLapStart,
            float surfaceTractionMod,
            ref float longitudinalGripFactor)
        {
            if (reverseBlockedAtLapStart)
            {
                _speedDiff = 0f;
                _lastDriveRpm = 0f;
                return;
            }

            var steeringCommandAccel = ComputeGripSteeringCommand(speedMpsCurrent);

            var steerRadAccel = (float)(Math.PI / 180.0) * (_maxSteerDeg * steeringCommandAccel);
            var curvatureAccel = (float)Math.Tan(steerRadAccel) / _wheelbaseM;
            var desiredLatAccel = curvatureAccel * speedMpsCurrent * speedMpsCurrent;
            var desiredLatAccelAbs = Math.Abs(desiredLatAccel);
            var grip = _tireGripCoefficient * surfaceTractionMod * _lateralGripCoefficient;
            var maxLatAccel = grip * 9.80665f;
            var speedFactor = ComputeSteeringSpeedFactor(speedMpsCurrent);
            var lateralRatioScale = 1.0f + (0.85f * speedFactor);
            var lateralRatio = maxLatAccel > 0f ? Math.Min(1.0f, desiredLatAccelAbs / (maxLatAccel * lateralRatioScale)) : 0f;
            longitudinalGripFactor = (float)Math.Sqrt(Math.Max(0.0, 1.0 - (lateralRatio * lateralRatio)));
            var longitudinalGripFloor = ComputeLongitudinalGripFloor(speedMpsCurrent);
            if (longitudinalGripFactor < longitudinalGripFloor)
                longitudinalGripFactor = longitudinalGripFloor;
            if (longitudinalGripFactor > 1.0f)
                longitudinalGripFactor = 1.0f;

            var driveRpm = CalculateDriveRpm(speedMpsCurrent, throttle);
            var engineTorque = CalculateEngineTorqueNm(driveRpm) * throttle * _powerFactor;
            var gearRatio = inReverse ? _reverseGearRatio : _engine.GetGearRatio(GetDriveGear());
            var wheelTorque = engineTorque * gearRatio * _finalDriveRatio * _drivetrainEfficiency;
            var wheelForce = wheelTorque / _wheelRadiusM;
            var tractionLimit = _tireGripCoefficient * surfaceTractionMod * _massKg * 9.80665f;
            if (wheelForce > tractionLimit)
                wheelForce = tractionLimit;
            wheelForce *= longitudinalGripFactor;
            wheelForce *= (_factor1 / 100f);
            if (inReverse)
                wheelForce *= _reversePowerFactor;

            var dragForce = 0.5f * 1.225f * _dragCoefficient * _frontalAreaM2 * speedMpsCurrent * speedMpsCurrent;
            var rollingForce = _rollingResistanceCoefficient * _massKg * 9.80665f;
            var netForce = wheelForce - dragForce - rollingForce;
            var accelMps2 = netForce / _massKg;
            var newSpeedMps = speedMpsCurrent + (accelMps2 * elapsed);
            if (newSpeedMps < 0f)
                newSpeedMps = 0f;

            _speedDiff = (newSpeedMps - speedMpsCurrent) * 3.6f;
            _lastDriveRpm = CalculateDriveRpm(newSpeedMps, throttle);
            if (_backfirePlayed)
                _backfirePlayed = false;
        }

        private void ApplyCoastDecel(float elapsed)
        {
            var surfaceDecelMod = _deceleration > 0f ? _currentDeceleration / _deceleration : 1.0f;
            var brakeInput = Math.Max(0f, Math.Min(100f, -_currentBrake)) / 100f;
            var brakeDecel = CalculateBrakeDecel(brakeInput, surfaceDecelMod);
            var engineBrakeDecel = CalculateEngineBrakingDecel(surfaceDecelMod);
            var totalDecel = _thrust < -10 ? (brakeDecel + engineBrakeDecel) : engineBrakeDecel;
            _speedDiff = -totalDecel * elapsed;
            _lastDriveRpm = 0f;
        }

        private void ClampSpeedAndTransmission(
            float elapsed,
            float throttle,
            bool inReverse,
            bool reverseBlockedAtLapStart,
            float surfaceTractionMod,
            float longitudinalGripFactor)
        {
            _speed += _speedDiff;
            if (_speed > _topSpeed)
                _speed = _topSpeed;
            if (_speed < 0f)
                _speed = 0f;
            if (!IsFinite(_speed))
            {
                _speed = 0f;
                _speedDiff = 0f;
            }

            if (!IsFinite(_lastDriveRpm))
                _lastDriveRpm = _idleRpm;

            if (reverseBlockedAtLapStart && _thrust > 10f)
            {
                _speed = 0f;
                _speedDiff = 0f;
                _lastDriveRpm = 0f;
            }

            if (inReverse)
            {
                var reverseMax = Math.Max(5.0f, _reverseMaxSpeedKph);
                if (_speed > reverseMax)
                    _speed = reverseMax;
                return;
            }

            if (_manualTransmission)
            {
                var gearMax = _engine.GetGearMaxSpeedKmh(_gear);
                if (_speed > gearMax)
                    _speed = gearMax;
            }
            else
            {
                UpdateAutomaticGear(elapsed, _speed / 3.6f, throttle, surfaceTractionMod, longitudinalGripFactor);
            }
        }

        private void SyncEngineFromSpeed(float elapsed)
        {
            _engine.SyncFromSpeed(_speed, GetDriveGear(), elapsed, _currentThrottle);
            if (_lastDriveRpm > 0f && _lastDriveRpm > _engine.Rpm)
                _engine.OverrideRpm(_lastDriveRpm);
        }

        private void UpdateBackfireStateAfterDrive()
        {
            if (_thrust > 0)
                return;

            if (!AnyBackfirePlaying() && !_backfirePlayed && Algorithm.RandomInt(5) == 1)
                PlayRandomBackfire();
            _backfirePlayed = true;
        }

        private void IntegrateVehiclePosition(float elapsed, float currentLapStart)
        {
            var speedMps = _speed / 3.6f;
            var longitudinalDelta = speedMps * elapsed;
            if (_gear == ReverseGear)
            {
                var nextPositionY = _positionY - longitudinalDelta;
                if (nextPositionY < currentLapStart)
                    nextPositionY = currentLapStart;
                if (nextPositionY < 0f)
                    nextPositionY = 0f;
                _positionY = nextPositionY;
            }
            else
            {
                _positionY += longitudinalDelta;
            }

            var surfaceMultiplier = _surface == TrackSurface.Snow ? 1.44f : 1.0f;
            var bikeLikeFactor = ComputeBikeLikeFactor();
            var steeringSpeedFactor = ComputeSteeringSpeedFactor(speedMps);
            var steeringCommandLat = ComputeSteeringCommand(speedMps, bikeLikeFactor);
            var commandBoost = 1.0f + ((0.95f + (0.85f * bikeLikeFactor)) * steeringSpeedFactor);
            steeringCommandLat *= commandBoost;
            if (steeringCommandLat > 2.45f)
                steeringCommandLat = 2.45f;
            else if (steeringCommandLat < -2.45f)
                steeringCommandLat = -2.45f;
            var steerRadLat = (float)(Math.PI / 180.0) * (_maxSteerDeg * steeringCommandLat);
            var curvatureLat = (float)Math.Tan(steerRadLat) / _wheelbaseM;
            var surfaceTractionModLat = _surfaceTractionFactor > 0f ? _currentSurfaceTractionFactor / _surfaceTractionFactor : 1.0f;
            var gripLat = _tireGripCoefficient * surfaceTractionModLat * _lateralGripCoefficient;
            var maxLatAccelLat = gripLat * 9.80665f;
            var desiredLatAccelLat = curvatureLat * speedMps * speedMps;
            var massFactor = (float)Math.Sqrt(1500f / _massKg);
            if (massFactor > 3.0f)
                massFactor = 3.0f;
            var stabilityScale = 1.0f - (_highSpeedStability * (speedMps / StabilitySpeedRef) * massFactor);
            if (stabilityScale < 0.2f)
                stabilityScale = 0.2f;
            else if (stabilityScale > 1.0f)
                stabilityScale = 1.0f;
            var stabilityRelief = 1.0f + (0.45f * steeringSpeedFactor);
            var responseTime = (BaseLateralSpeed / 20.0f) * (0.78f + (1.28f * steeringSpeedFactor));
            var highSpeedAgility = 1.0f + ((0.90f + (0.95f * bikeLikeFactor)) * steeringSpeedFactor);
            var maxLatSpeed = maxLatAccelLat * responseTime * stabilityScale * highSpeedAgility;
            maxLatSpeed *= stabilityRelief;
            var desiredLatSpeed = desiredLatAccelLat * responseTime;
            if (desiredLatSpeed > maxLatSpeed)
                desiredLatSpeed = maxLatSpeed;
            else if (desiredLatSpeed < -maxLatSpeed)
                desiredLatSpeed = -maxLatSpeed;
            var lateralSpeed = desiredLatSpeed * surfaceMultiplier;
            _positionX += lateralSpeed * elapsed;
        }

        private float ComputeSteeringCommand(float speedMps)
        {
            return ComputeSteeringCommand(speedMps, ComputeBikeLikeFactor());
        }

        private float ComputeSteeringCommand(float speedMps, float bikeLikeFactor)
        {
            var baseCommand = (_currentSteering / 100.0f) * _steering;
            var steeringGain = ComputeSteeringGain(speedMps, bikeLikeFactor);
            var scaled = baseCommand * steeringGain;
            if (scaled > 1.95f)
                return 1.95f;
            if (scaled < -1.95f)
                return -1.95f;
            return scaled;
        }

        private float ComputeGripSteeringCommand(float speedMps)
        {
            var baseCommand = (_currentSteering / 100.0f) * _steering;
            var speedFactor = ComputeSteeringSpeedFactor(speedMps);
            var gripGain = 1.0f + (0.32f * speedFactor);
            var scaled = baseCommand * gripGain;
            if (scaled > 1.55f)
                return 1.55f;
            if (scaled < -1.55f)
                return -1.55f;
            return scaled;
        }

        private static float ComputeSteeringSpeedFactor(float speedMps)
        {
            const float startMps = 90.0f / 3.6f;
            const float endMps = 180.0f / 3.6f;
            if (speedMps <= startMps)
                return 0f;
            if (speedMps >= endMps)
                return 1f;
            return (speedMps - startMps) / (endMps - startMps);
        }

        private static float ComputeSteeringGain(float speedMps, float bikeLikeFactor)
        {
            var speedFactor = ComputeSteeringSpeedFactor(speedMps);
            var shapedFactor = (float)Math.Pow(speedFactor, 0.85);
            var baseGain = 1.0f + (0.70f * shapedFactor);
            var bikeGain = 1.0f + (0.45f * bikeLikeFactor * shapedFactor);
            return baseGain * bikeGain;
        }

        private float ComputeBikeLikeFactor()
        {
            var widthFactor = NormalizeRange(1.05f - _widthM, 0f, 0.42f);
            var wheelbaseFactor = NormalizeRange(1.95f - _wheelbaseM, 0f, 0.70f);
            var massFactor = NormalizeRange(420f - _massKg, 0f, 280f);
            var combined = (0.50f * widthFactor) + (0.30f * wheelbaseFactor) + (0.20f * massFactor);
            if (combined < 0f)
                return 0f;
            if (combined > 1f)
                return 1f;
            return combined;
        }

        private static float NormalizeRange(float value, float min, float max)
        {
            if (max <= min)
                return 0f;
            if (value <= min)
                return 0f;
            if (value >= max)
                return 1f;
            return (value - min) / (max - min);
        }

        private static float ComputeLongitudinalGripFloor(float speedMps)
        {
            var speedFactor = ComputeSteeringSpeedFactor(speedMps);
            return 0.66f + (0.20f * speedFactor);
        }
    }
}
