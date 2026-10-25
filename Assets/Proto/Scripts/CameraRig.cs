using UnityEngine;
using UnityEngine.InputSystem;

namespace LifeSimProto
{
    /// <summary>
    /// 自由に動かせる視点。
    /// 右ドラッグ：回す（下向きにドラッグすると床の下から見上げられる） / ホイール：近づく・離れる / WASD：移動 / 中ドラッグ：平行移動 / Q・E：左右に回す / R・F：上下
    /// キャラをダブルクリックで追いかける（Escで解除）。時間を止めていても動かせる。
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public Vector3 pivot = new Vector3(0f, 1.5f, -2f);
        public float yaw = 0f;
        public float pitch = 48f;
        public float distance = 24f;
        public Transform follow;

        const float MinDist = 1.5f, MaxDist = 45f;

        /// <summary>その階を見る（0＝1階 … 3＝屋上）。見ている階より上の階は隠れる（LoungeWorld.UpdateFloorView）。</summary>
        public void LookAtFloor(int floor)
        {
            follow = null;
            pivot.y = LoungeWorld.FloorY(Mathf.Clamp(floor, 0, 3)) + 0.8f;
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            var mouse = Mouse.current;
            var kb = Keyboard.current;

            if (mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue();
                if (mouse.rightButton.isPressed)
                {
                    yaw += delta.x * 0.25f;
                    pitch = Mathf.Clamp(pitch - delta.y * 0.25f, -70f, 88f); // マイナスは下から見上げる
                }
                if (mouse.middleButton.isPressed)
                {
                    Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
                    Vector3 fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                    float k = distance * 0.0025f;
                    pivot -= right * delta.x * k + fwd * delta.y * k;
                    follow = null;
                }
                float scroll = mouse.scroll.ReadValue().y;
                if (SimHud.Instance != null && SimHud.Instance.PointerOverHud()) scroll = 0f; // 画面の欄（できごとなど）の上では、ホイールは欄を動かす
                if (scroll > 0.01f) distance *= 0.88f;
                else if (scroll < -0.01f) distance *= 1.14f;
                distance = Mathf.Clamp(distance, MinDist, MaxDist);
            }

            if (kb != null)
            {
                Vector3 move = Vector3.zero;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move += Vector3.forward;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move += Vector3.back;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move += Vector3.left;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move += Vector3.right;
                if (kb.qKey.isPressed) yaw -= 70f * dt;
                if (kb.eKey.isPressed) yaw += 70f * dt;
                if (kb.rKey.isPressed) { pivot.y += 3f * dt; follow = null; }
                if (kb.fKey.isPressed) { pivot.y -= 3f * dt; follow = null; }
                if (move.sqrMagnitude > 0f)
                {
                    float spd = (2.5f + distance * 0.35f) * (kb.leftShiftKey.isPressed ? 2.5f : 1f);
                    pivot += Quaternion.Euler(0f, yaw, 0f) * move.normalized * spd * dt;
                    follow = null;
                }
                if (kb.escapeKey.wasPressedThisFrame) follow = null;
            }

            if (follow != null)
            {
                Vector3 want = follow.position + Vector3.up * 1.0f;
                pivot = Vector3.Lerp(pivot, want, 1f - Mathf.Exp(-dt * 5f));
            }

            pivot.x = Mathf.Clamp(pivot.x, -24f, 24f);
            pivot.z = Mathf.Clamp(pivot.z, -28f, 38f); // 前庭・渡り廊下・中庭まで見られるように
            pivot.y = Mathf.Clamp(pivot.y, 0.2f, LoungeWorld.RoofY + 0.9f);

            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pos = pivot - rot * Vector3.forward * distance;
            if (pos.y < -12f) pos.y = -12f; // 床の下まで下がれる（床は下から見ると透ける）
            transform.position = pos;
            transform.rotation = Quaternion.LookRotation(pivot - pos);
        }
    }
}
