# 한키 클라우드 동기화 Gap 분석

이 문서는 제안된 `hanki_shortcuts` migration을 검토한 결과다. SQL은 실행하지 않았고,
웹 단축어 관리 UI도 활성화하지 않았으며 Windows 동기화 구현도 추가하지 않았다.

## 모델 비교

| Windows 로컬 | 제안 SQL | 비고 |
|---|---|---|
| string GUID `Id` | uuid `id` | 형식 변환 규칙 필요 |
| `TriggerText` 1–100 rune | `trigger_text` 1–100 char | Unicode 결합문자 기준 확인 필요 |
| `ReplacementText` 1–10,000 rune | `replacement_text` 1–10,000 char | 민감 문장 포함 가능 |
| nullable title ≤120 | nullable title 1–120 | 공백 title 정규화 필요 |
| favorite, usage count, last used | 대응 column 존재 | usage 병합 규칙 미정 |
| created/updated UTC | timestamptz | server/client clock 우선순위 미정 |

## 현재 제안의 장점

- `user_id`는 `auth.users(id)`를 참조하고 계정 삭제 시 cascade한다.
- RLS가 활성화되고 authenticated에게만 권한을 부여한다.
- select/insert/update/delete 모두 `(select auth.uid()) = user_id`로 본인 행만 허용한다.
- `(user_id, trigger_text)` unique와 길이/공백/음수 사용 횟수 제약이 있다.
- `updated_at` trigger와 사용자별 favorite/updated index가 있다.

## 출시 전 결정이 필요한 Gap

1. **동기화 프로토콜**: tombstone 또는 삭제 journal이 없어 오프라인 삭제가 다른 기기에
   전파됐는지 알 수 없다.
2. **충돌 해결**: 양쪽 수정, 여러 기기, clock skew에서 last-write-wins만으로는 데이터
   손실 가능성이 있다. revision/etag와 명시적 conflict copy가 필요하다.
3. **ID와 중복**: 로컬 GUID를 server id로 보존할지, trigger unique 충돌 시 rename/merge/
   reject할지 정해야 한다.
4. **대량 처리**: pagination, batch upsert, rate limit, 초기 sync 진행/취소/재시도와
   부분 실패 복구가 없다.
5. **검증 일치**: .NET RuneCount와 PostgreSQL `char_length`, 줄바꿈·control character,
   Unicode normalization 규칙을 공유해야 한다.
6. **민감도**: replacement에는 주소·응답 문안 등 민감 정보가 들어갈 수 있다. Supabase
   저장 암호화만으로 충분한지, client-side end-to-end encryption과 key 복구 정책이
   필요한지 제품 결정을 해야 한다.
7. **보존·삭제**: 로그아웃은 로컬 auth session만 삭제한다. 서버 단축어 전체 삭제,
   계정 탈퇴, backup/export, 보존 기간과 지원 절차가 필요하다.
8. **관찰 가능성**: 내용 없는 메타데이터 중심 로그, retry budget, sync 상태 진단이 필요하다.

결론: RLS 초안은 본인 행 격리의 출발점으로 타당하지만 삭제·충돌·암호화·정책이 결정되지
않아 production migration과 동기화 UI를 활성화하면 안 된다.
