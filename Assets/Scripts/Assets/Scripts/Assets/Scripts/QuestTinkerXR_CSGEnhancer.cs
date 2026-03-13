// QuestTinkerXR_CSGEnhancer.cs
// Adds live Boolean hole-cutting and object grab/move to the open-source TinkerXR Unity project.
// Drop this script on an empty GameObject in the scene.
// Requires: XR Interaction Toolkit + Oculus Integration (Meta XR SDK) already set up as in original repo.
// Uses free CSG library "CSG.NET" (include its DLLs in Plugins) or any compatible runtime-CSG.
// Controls (Meta Quest default mapping):
//   • Right Trigger  – grab / move selected object with ray.
//   • A button       – spawn cutter primitive (toggle Box / Cylinder with Thumbstick Right).
//   • B button       – execute SUBTRACT (hole) on selected object using current cutter, then destroys cutter.
//   • Thumbstick Up  – scale cutter +10%  |  Thumbstick Down – scale cutter –10%.
//   • Grip           – duplicate selected object.
//   • X button       – delete selected object.
// This is bare-bones and meant to extend existing functionality, not replace full UI.

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class QuestTinkerXR_CSGEnhancer : MonoBehaviour
{
    [Header("Refs")]
    public XRRayInteractor rightRay;
    public XRController     rightController;
    public Material         cutterMat;

    enum CutterKind { Box, Cylinder }
    CutterKind currentKind = CutterKind.Box;
    Transform  cutter;
    Transform  grabbed;

    void Update()
    {
        if (!rightController || !rightRay) return;

        // Check what the ray is hitting
        rightRay.TryGetCurrent3DRaycastHit(out RaycastHit hit);

        // Trigger → grab & move
        rightController.inputDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool trig);
        if (trig && hit.transform && hit.transform.CompareTag("Editable"))
        {
            if (grabbed == null) grabbed = hit.transform;
            grabbed.position = hit.point;
        }
        else if (!trig) grabbed = null;

        // Thumbstick horizontal → toggle cutter type
        rightController.inputDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out Vector2 stick);
        if (stick.x > 0.5f)  currentKind = CutterKind.Cylinder;
        if (stick.x < -0.5f) currentKind = CutterKind.Box;

        // Thumbstick vertical → scale cutter
        if (cutter)
        {
            float scaleDelta = stick.y * Time.deltaTime;
            cutter.localScale += Vector3.one * scaleDelta;
        }

        // Button A → spawn cutter at hit point
        rightController.inputDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool aBtn);
        if (aBtn && cutter == null && hit.transform)
        {
            cutter = SpawnCutter(hit.point, currentKind);
        }

        // Button B → subtract
        rightController.inputDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bool bBtn);
        if (bBtn && cutter && hit.transform && hit.transform.CompareTag("Editable"))
        {
            PerformSubtract(hit.transform.gameObject, cutter.gameObject);
            Destroy(cutter.gameObject);
            cutter = null;
        }

        // Grip → duplicate
        rightController.inputDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.gripButton, out bool grip);
        if (grip && hit.transform && hit.transform.CompareTag("Editable"))
        {
            Duplicate(hit.transform);
        }

        // X button → delete (maps to primaryTouch on Oculus)
        rightController.inputDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryTouch, out bool xBtn);
        if (xBtn && hit.transform && hit.transform.CompareTag("Editable"))
        {
            Destroy(hit.transform.gameObject);
        }
    }

    Transform SpawnCutter(Vector3 pos, CutterKind kind)
    {
        GameObject go = kind == CutterKind.Box
            ? GameObject.CreatePrimitive(PrimitiveType.Cube)
            : GameObject.CreatePrimitive(PrimitiveType.Cylinder);

        go.transform.position = pos;
        go.transform.localScale = Vector3.one * 0.1f;
        go.GetComponent<Collider>().isTrigger = true;
        go.GetComponent<MeshRenderer>().material = cutterMat;
        return go.transform;
    }

    void PerformSubtract(GameObject target, GameObject cutterGO)
    {
        // Assumes both meshes are watertight. Replace CSG.Subtract below with the call
        // appropriate to the CSG library you imported (e.g., CSG.NET or SabreCSG runtime).
        var targetMesh = target.GetComponent<MeshFilter>().mesh;
        var cutterMesh = cutterGO.GetComponent<MeshFilter>().mesh;

        var res = CSG.CSG.Subtract(
            targetMesh,  target.transform.localToWorldMatrix,
            cutterMesh, cutterGO.transform.localToWorldMatrix);

        if (res != null)
        {
            targetMesh.Clear();
            targetMesh.vertices  = res.vertices;
            targetMesh.triangles = res.triangles;
            targetMesh.RecalculateNormals();
            targetMesh.RecalculateBounds();
        }
    }

    void Duplicate(Transform src)
    {
        var copy = Instantiate(src.gameObject,
                               src.position + new Vector3(0.05f, 0, 0.05f),
                               src.rotation,
                               src.parent);
        copy.tag = "Editable";
    }
}
