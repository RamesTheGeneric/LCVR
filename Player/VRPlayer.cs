﻿using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using System;
using UnityEngine.Animations.Rigging;
using UnityEngine.Rendering;

namespace LethalCompanyVR
{
    // Attach this to the main Player

    internal class VRPlayer : MonoBehaviour
    {
        private const float SCALE_FACTOR = 1.6f;

        private GameObject leftController;
        private GameObject rightController;

        private GameObject cameraContainer;
        private Camera mainCamera;

        private GameObject cameraAnchor;

        private Vector3 lastFrameHMDPosition = new Vector3(0, 0, 0);

        private void Awake()
        {
            try
            {
                Logger.LogDebug("Going to intialize XR Rig");

                // Disable base rig
                //Find("ScavengerModel/metarig").GetComponent<Animator>().enabled = false;

                // Disable base model shadows
                Find("ScavengerModel/LOD1").GetComponent<SkinnedMeshRenderer>().enabled = false;

                // Make sure the player doesn't rotate
                transform.rotation = Quaternion.identity;

                // Create XR stuff
                var scavengerModel = Find("ScavengerModel");
                cameraContainer = Find("ScavengerModel/metarig/CameraContainer");
                mainCamera = Find("ScavengerModel/metarig/CameraContainer/MainCamera").GetComponent<Camera>();
                cameraAnchor = new GameObject("Camera Anchor");

                // Unparent camera container
                cameraContainer.transform.parent = null;
                cameraContainer.transform.localPosition = Vector3.zero;
                cameraContainer.transform.localRotation = Quaternion.Euler(0, 0, 0);
                cameraContainer.transform.localScale = Vector3.one;

                // Create HMD tracker
                var cameraPoseDriver = mainCamera.gameObject.AddComponent<TrackedPoseDriver>();
                cameraPoseDriver.positionAction = Actions.XR_HeadPosition;
                cameraPoseDriver.rotationAction = Actions.XR_HeadRotation;
                cameraPoseDriver.trackingStateInput = new InputActionProperty(Actions.XR_HeadTrackingState);

                // Create controller objects
                rightController = new GameObject("Right Controller");
                leftController = new GameObject("Left Controller");

                // And mount to camera container
                rightController.transform.parent = cameraContainer.transform;
                leftController.transform.parent = cameraContainer.transform;

                // Left hand tracking
                var rightHandPoseDriver = rightController.AddComponent<TrackedPoseDriver>();
                rightHandPoseDriver.positionAction = Actions.XR_RightHand_Position;
                rightHandPoseDriver.rotationAction = Actions.XR_RightHand_Rotation;
                rightHandPoseDriver.trackingStateInput = new InputActionProperty(Actions.XR_RightHand_TrackingState);

                // Right hand tracking
                var leftHandPoseDriver = leftController.AddComponent<TrackedPoseDriver>();
                leftHandPoseDriver.positionAction = Actions.XR_LeftHand_Position;
                leftHandPoseDriver.rotationAction = Actions.XR_LeftHand_Rotation;
                leftHandPoseDriver.trackingStateInput = new InputActionProperty(Actions.XR_LeftHand_TrackingState);

                // Set up IK Rig VR targets
                var headVRTarget = new GameObject("Head VR Target");
                var rightHandVRTarget = new GameObject("Right Hand VR Target");
                var leftHandVRTarget = new GameObject("Left Hand VR Target");

                headVRTarget.transform.parent = mainCamera.transform;
                rightHandVRTarget.transform.parent = rightController.transform;
                leftHandVRTarget.transform.parent = leftController.transform;

                // Head defintely does need to have offsets (in this case an offset of 0, 0, 0)
                headVRTarget.transform.localPosition = Vector3.zero;

                rightHandVRTarget.transform.localPosition = new Vector3(0.012f, 0.016f, -0.146f);
                rightHandVRTarget.transform.localRotation = Quaternion.Euler(0, 90, 90 + 45);

                leftHandVRTarget.transform.localPosition = new Vector3(-0.012f, 0.016f, -0.146f);
                leftHandVRTarget.transform.localRotation = Quaternion.Euler(180, 90, 90 - 45);

                if (true)
                {
                    // TODO: Remove debug controller objects
                    GameObject.Instantiate(AssetManager.rightHand).transform.parent = rightController.transform;
                    GameObject.Instantiate(AssetManager.leftHand).transform.parent = leftController.transform;
                }

                // Disable built-in constraints
                GameObject.Destroy(Find("ScavengerModel/metarig/ScavengerModelArmsOnly/metarig/spine.003/RigArms/RightArm").GetComponent<ChainIKConstraint>());
                GameObject.Destroy(Find("ScavengerModel/metarig/ScavengerModelArmsOnly/metarig/spine.003/RigArms/LeftArm").GetComponent<ChainIKConstraint>());

                // Set up rigging
                var model = Find("ScavengerModel/metarig/ScavengerModelArmsOnly", true);
                var modelMetarig = Find("ScavengerModel/metarig/ScavengerModelArmsOnly/metarig", true);

                Find("ScavengerModel/metarig/ScavengerModelArmsOnly/metarig/spine.003/RigArms", true);

                var rig = modelMetarig.AddComponent<Rig>();
                var rigBuilder = model.AddComponent<RigBuilder>();
                var rigFollow = model.AddComponent<IKRigFollowVRRig>();

                rigBuilder.layers.Add(new RigLayer(rig, true));

                var head = Find("ScavengerModel/metarig/Rig 1/LookHead");
                var rightArm = Find("ScavengerModel/metarig/ScavengerModelArmsOnly/metarig/spine.003/RigArms/RightArm", true);
                var leftArm = Find("ScavengerModel/metarig/ScavengerModelArmsOnly/metarig/spine.003/RigArms/LeftArm", true);

                // Setting up the head

                var headIKTarget = Find("ScavengerModel/metarig/ScavengerModelArmsOnly/metarig/spine.003", true);
                var headTarget = new GameObject("Head_Target");

                headTarget.transform.parent = head.transform;

                var headConstraint = head.AddComponent<MultiParentConstraint>();
                headConstraint.data.constrainedPositionXAxis = true;
                headConstraint.data.constrainedPositionYAxis = true;
                headConstraint.data.constrainedPositionZAxis = true;
                headConstraint.data.constrainedRotationXAxis = true;
                headConstraint.data.constrainedRotationYAxis = true;
                headConstraint.data.constrainedRotationZAxis = true;
                headConstraint.data.constrainedObject = headIKTarget.transform;
                headConstraint.data.sourceObjects = new WeightedTransformArray(1);
                headConstraint.data.sourceObjects.SetTransform(0, headTarget.transform);
                headConstraint.data.sourceObjects.SetWeight(0, 1);

                headConstraint.Reset();

                rigFollow.head = new IKRigFollowVRRig.VRMap()
                {
                    ikTarget = headTarget.transform,
                    vrTarget = headVRTarget.transform,
                    trackingPositionOffset = Vector3.zero,
                    trackingRotationOffset = Vector3.zero,
                };

                // Setting up the right arm

                var rightArmRoot = Find("ScavengerModel/metarig/ScavengerModelArmsOnly/metarig/spine.003/shoulder.R/arm.R_upper");
                var rightArmMid = Find("ScavengerModel/metarig/ScavengerModelArmsOnly/metarig/spine.003/shoulder.R/arm.R_upper/arm.R_lower");
                var rightArmTip = Find("ScavengerModel/metarig/ScavengerModelArmsOnly/metarig/spine.003/shoulder.R/arm.R_upper/arm.R_lower/hand.R");

                var rightArmTarget = Find("ScavengerModel/metarig/ScavengerModelArmsOnly/metarig/spine.003/RigArms/RightArm/ArmsRightArm_target");
                var rightArmHint = Find("ScavengerModel/metarig/ScavengerModelArmsOnly/metarig/spine.003/RigArms/RightArm/RightArm_hint");

                rightArmHint.transform.localPosition = new Vector3(2.5f, -2f, -1f);

                var rightArmConstraint = rightArm.AddComponent<TwoBoneIKConstraint>();
                rightArmConstraint.data.root = rightArmRoot.transform;
                rightArmConstraint.data.mid = rightArmMid.transform;
                rightArmConstraint.data.tip = rightArmTip.transform;
                rightArmConstraint.data.target = rightArmTarget.transform;
                rightArmConstraint.data.hint = rightArmHint.transform;
                rightArmConstraint.data.hintWeight = 1;
                rightArmConstraint.data.targetRotationWeight = 1;
                rightArmConstraint.data.targetPositionWeight = 1;

                rigFollow.rightHand = new IKRigFollowVRRig.VRMap()
                {
                    ikTarget = rightArmTarget.transform,
                    vrTarget = rightHandVRTarget.transform,
                    trackingPositionOffset = Vector3.zero,
                    trackingRotationOffset = Vector3.zero
                };

                // Setting up the left arm

                var leftArmRoot = Find("ScavengerModel/metarig/ScavengerModelArmsOnly/metarig/spine.003/shoulder.L/arm.L_upper");
                var leftArmMid = Find("ScavengerModel/metarig/ScavengerModelArmsOnly/metarig/spine.003/shoulder.L/arm.L_upper/arm.L_lower");
                var leftArmTip = Find("ScavengerModel/metarig/ScavengerModelArmsOnly/metarig/spine.003/shoulder.L/arm.L_upper/arm.L_lower/hand.L");

                var leftArmTarget = Find("ScavengerModel/metarig/ScavengerModelArmsOnly/metarig/spine.003/RigArms/LeftArm/ArmsLeftArm_target");
                var leftArmHint = Find("ScavengerModel/metarig/ScavengerModelArmsOnly/metarig/spine.003/RigArms/LeftArm/LeftArm_hint");

                leftArmHint.transform.localPosition = new Vector3(-1f, -2f, -1f);

                var leftArmConstraint = leftArm.AddComponent<TwoBoneIKConstraint>();
                leftArmConstraint.data.root = leftArmRoot.transform;
                leftArmConstraint.data.mid = leftArmMid.transform;
                leftArmConstraint.data.tip = leftArmTip.transform;
                leftArmConstraint.data.target = leftArmTarget.transform;
                leftArmConstraint.data.hint = leftArmHint.transform;
                leftArmConstraint.data.hintWeight = 1;
                leftArmConstraint.data.targetRotationWeight = 1;
                leftArmConstraint.data.targetPositionWeight = 1;

                rigFollow.leftHand = new IKRigFollowVRRig.VRMap()
                {
                    ikTarget = leftArmTarget.transform,
                    vrTarget = leftHandVRTarget.transform,
                    trackingPositionOffset = Vector3.zero,
                    trackingRotationOffset = Vector3.zero
                };

                rigFollow.headBodyPositionOffset = new Vector3(0, -2.25f, 0);
                
                // Notify rig about our new constraints
                rigBuilder.Build();

                // Add controller interactor
                var right = rightController.AddComponent<VRController>();
                right.Initialize(this);

                Logger.LogDebug("Initialized XR Rig");
            }
            // TODO: Remove try/catch in prod
            catch (Exception ex)
            {
                Logger.LogError(ex.Message);
                Logger.LogError(ex.StackTrace);
            }
        }

        private void Update()
        {
            var movement = mainCamera.transform.localPosition - lastFrameHMDPosition;
            movement.y = 0;

            transform.position += new Vector3(movement.x * SCALE_FACTOR, 0, movement.z * SCALE_FACTOR);
         
            // Hmmmm?
            //GetComponent<CharacterController>().Move(movement);

            cameraAnchor.transform.position = new Vector3(transform.position.x - mainCamera.transform.localPosition.x * SCALE_FACTOR, 0, transform.position.z - mainCamera.transform.localPosition.z * SCALE_FACTOR);
            //cameraAnchor.transform.localPosition = new Vector3(-mainCamera.transform.localPosition.x, 0, -mainCamera.transform.localPosition.z);
            //cameraAnchor.transform.rotation = Quaternion.Euler(0, -transform.eulerAngles.y, 0);

            cameraContainer.transform.position = cameraAnchor.transform.position;
            cameraContainer.transform.rotation = Quaternion.Euler(0, 0, 0);
            cameraContainer.transform.localScale = Vector3.one * SCALE_FACTOR;

            transform.rotation = Quaternion.Euler(0, mainCamera.transform.eulerAngles.y, 0);

            lastFrameHMDPosition = mainCamera.transform.localPosition;
        }

        private GameObject Find(string name, bool resetLocalPosition = false)
        {
            var @object = transform.Find(name).gameObject;
            if (@object == null) return null;

            if (resetLocalPosition)
                @object.transform.localPosition = Vector3.zero;

            return @object;
        }

        public static void VibrateController(XRNode hand, float duration, float amplitude)
        {
            UnityEngine.XR.InputDevice device = InputDevices.GetDeviceAtXRNode(hand);

            if (device != null && device.TryGetHapticCapabilities(out HapticCapabilities capabilities) && capabilities.supportsImpulse)
            {
                device.SendHapticImpulse(0, amplitude, duration);
            }
        }
    }

    internal class IKRigFollowVRRig : MonoBehaviour
    {
        [Serializable]
        public class VRMap
        {
            public Transform vrTarget;
            public Transform ikTarget;
            public Vector3 trackingPositionOffset;
            public Vector3 trackingRotationOffset;

            public void Map()
            {
                ikTarget.position = vrTarget.TransformPoint(trackingPositionOffset);
                ikTarget.rotation = vrTarget.rotation * Quaternion.Euler(trackingRotationOffset);
            }
        }

        [Range(0, 1)]
        public float turnSmoothness = 0.1f;
        public VRMap head;
        public VRMap leftHand;
        public VRMap rightHand;

        public Vector3 headBodyPositionOffset;
        public float headBodyYawOffset;

        private void LateUpdate()
        {
            transform.position = head.ikTarget.position + headBodyPositionOffset;
            float yaw = head.vrTarget.eulerAngles.y;
            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(transform.eulerAngles.x, yaw, transform.eulerAngles.z), turnSmoothness);

            head.Map();
            leftHand.Map();
            rightHand.Map();
        }
    }
}
