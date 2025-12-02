using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace shotgunforcedoor
{
    // Duckov 모드 엔트리
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        protected override void OnAfterSetup()
        {
            try
            {
                Debug.Log("[ShotgunForceDoor] OnAfterSetup - Manager 생성");

                GameObject root = new GameObject("ShotgunForceDoorRoot");
                UnityEngine.Object.DontDestroyOnLoad(root);
                root.AddComponent<ShotgunForceDoorManager>();
            }
            catch (Exception ex)
            {
                Debug.Log("[ShotgunForceDoor] OnAfterSetup 예외: " + ex);
            }
        }

        protected override void OnBeforeDeactivate()
        {
            Debug.Log("[ShotgunForceDoor] OnBeforeDeactivate - 언로드");
        }
    }

    public class ShotgunForceDoorManager : MonoBehaviour
    {
        private static ShotgunForceDoorManager _instance;

        // 문을 인식할 최대 거리
        private float _maxDistance = 6f;

        private void Awake()
        {
            if (_instance != null)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            Debug.Log("[ShotgunForceDoor] Manager Awake");
        }

        private void Update()
        {
            // 발사 버튼 눌렀을 때만 동작
            if (!Input.GetButtonDown("Fire1") && !Input.GetMouseButtonDown(0))
                return;

            TryOpenInteractableDoor();
        }

        private void TryOpenInteractableDoor()
        {
            Camera cam = Camera.main;
            if (cam == null)
                return;

            Vector3 origin = cam.transform.position;
            Vector3 dir = cam.transform.forward;

            RaycastHit hit;
            if (!Physics.Raycast(origin, dir, out hit, _maxDistance, ~0, QueryTriggerInteraction.Ignore))
                return;

            if (hit.collider == null)
                return;

            GameObject target = hit.collider.gameObject;

            if (TryCallDoorInteract(target, hit))
            {
                Debug.Log("[ShotgunForceDoor] 상호작용 문을 총으로 열었습니다.");
            }
        }

        // 맞은 오브젝트(및 부모들)에 붙은 "Door 계열 컴포넌트"에서 Interact / Use / Open 류 메서드를 호출
        private bool TryCallDoorInteract(GameObject hitObj, RaycastHit hit)
        {
            if (hitObj == null) return false;

            CharacterMainControl player = FindObjectOfType<CharacterMainControl>();

            Transform t = hitObj.transform;
            for (int depth = 0; depth < 5 && t != null; depth++)
            {
                MonoBehaviour[] comps = t.GetComponents<MonoBehaviour>();
                for (int i = 0; i < comps.Length; i++)
                {
                    MonoBehaviour comp = comps[i];
                    if (comp == null) continue;

                    Type type = comp.GetType();
                    string typeName = type.Name.ToLower();

                    // 문/게이트 계열 컴포넌트만 대상으로
                    if (!(typeName.Contains("door") ||
                          typeName.Contains("gate") ||
                          typeName.Contains("entrance")))
                        continue;

                    if (TryInvokeDoorMethods(comp, player))
                    {
                        Debug.Log("[ShotgunForceDoor] 문 컴포넌트 호출: " + type.Name + " (" + t.name + ")");
                        return true;
                    }
                }

                t = t.parent;
            }

            return false;
        }

        // 해당 컴포넌트에서 자주 쓰일 법한 메서드명을 찾아 호출
        private bool TryInvokeDoorMethods(MonoBehaviour comp, CharacterMainControl player)
        {
            if (comp == null) return false;

            Type type = comp.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            // 우선순위 높은 이름부터 시도
            string[] candidateNames = new string[]
            {
                "TryInteract",
                "Interact",
                "OnInteract",
                "Use",
                "TryUse",
                "OpenDoor",
                "Open",
                "ToggleDoor",
                "Toggle"
            };

            MethodInfo[] methods = type.GetMethods(flags);

            for (int i = 0; i < candidateNames.Length; i++)
            {
                string cand = candidateNames[i];

                for (int j = 0; j < methods.Length; j++)
                {
                    MethodInfo m = methods[j];
                    if (m == null) continue;

                    if (!m.Name.Equals(cand, StringComparison.OrdinalIgnoreCase))
                        continue;

                    ParameterInfo[] ps = m.GetParameters();
                    object[] args = null;

                    try
                    {
                        if (ps.Length == 0)
                        {
                            // 매개변수 없는 Interact()
                            args = null;
                        }
                        else if (ps.Length == 1)
                        {
                            Type pt = ps[0].ParameterType;

                            // CharacterMainControl / 그 파생형을 요구하면 플레이어 전달
                            if (player != null && pt.IsAssignableFrom(typeof(CharacterMainControl)))
                            {
                                args = new object[] { player };
                            }
                            // bool 하나 받는 경우 true 전달 (예: Interact(bool isLocal))
                            else if (pt == typeof(bool))
                            {
                                args = new object[] { true };
                            }
                            else
                            {
                                // 기타 형태는 지원 안 함
                                continue;
                            }
                        }
                        else
                        {
                            // 매개변수 2개 이상이면 패스
                            continue;
                        }

                        m.Invoke(comp, args);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Debug.Log("[ShotgunForceDoor] 메서드 호출 예외: " + type.Name + "." + m.Name + " - " + ex.Message);
                        // 다른 메서드 계속 시도
                    }
                }
            }

            return false;
        }
    }
}
