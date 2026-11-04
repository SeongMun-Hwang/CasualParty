# 리더보드 점수 미갱신 조사

## 기기에서 확인한 사실

- Google Play Games 인증 성공 로그가 있다.
- 로컬 최고 기록은 블록도쿠 88점, 10SUM 20점이며 pendingScores.json은 빈 목록이었다.
- 블록도쿠·10SUM의 리더보드 ID는 설정되어 있다.
- 게임 매니저는 자연 게임 종료 시 SubmitScore를 호출한다. 중도 새로고침은 점수를 전송하지 않는다.

## 확인된 전송 처리 결함

현재 설치된 Unity SDK 소스의 AndroidClient.SubmitScore는 Java의 submitScore를 호출한 직후 true 콜백을 호출한다. 이 콜백은 서버 전송 완료를 의미하지 않는다. 기존 PlayGamesService는 이를 서버 성공으로 간주해 전송 대기 기록을 삭제했다. 따라서 대기열이 비었다는 사실만으로 서버 반영을 확정할 수 없었다.

이 결함은 확인했지만, 사용자가 보고한 순위 미표시의 유일한 원인이라고 확정하지는 않았다. 서버 응답, 순위 정렬, 기록 공개 여부 등은 새 빌드에서 추가 확인해야 한다.

## 수정

- SDK 소스를 직접 수정하지 않고 LeaderboardScoreUploader를 추가했다.
- 공식 Android API submitScoreImmediate의 Task가 성공했을 때만 대기열을 삭제한다.
- 실패·취소·20초 타임아웃은 대기열에 유지한다. 오류 문자열과 대상 리더보드·점수를 로그에 기록한다.
- 한 게임의 전송 실패가 다른 게임의 전송 시도까지 막지 않도록 한다.
- 순위표를 열기 전에 진행 중인 업로드를 기다린다.
- 순위 창에 업로드 진행·완료·재시도 대기 상태를 표시한다.
- 기존 대기열의 안전한 갱신과 계정별 분리 정책은 유지한다.

## 새 빌드 확인 절차

1. 변경된 코드로 다시 빌드하고 기존 앱 위에 설치한다. 앱 데이터 삭제는 필요하지 않다.
2. 로그인 성공을 확인한다.
3. 10SUM에서 1점 이상 획득하고 120초 종료까지 기다린다.
4. 로그에서 아래 순서를 확인한다.

```text
[PlayGames] Score queued. Game=TenSum, score=...
[PlayGames] Upload started. Game=TenSum, board=..., score=...
[PlayGames] Server confirmed score. Game=TenSum, board=..., score=...
```

5. 실패라면 Upload unconfirmed 로그의 error 값을 확인한다. 기록은 pendingScores.json에 남아 다음 재시도에서 다시 전송된다.
6. 서버 확인이 성공했는데 공개 순위표가 비어 있다면 콘솔의 리더보드 정렬·허용 점수 범위·게시/테스트 상태·점수 변조 방지 설정, 플레이어 프로필 공개 설정을 추가 확인한다. 단순히 점수가 갱신되지 않는 경우에는 이전 서버 최고 기록보다 좋은 점수인지도 확인한다.

이전 코드에서 이미 삭제된 전송 대기 기록은 자동 복원할 수 없다. 새 빌드에서 다시 완료한 판으로 검증한다. 기존 로컬 최고 점수는 계정 소유권을 확인할 수 없어 자동으로 업로드하지 않는다.

공식 API: [LeaderboardsClient](https://developers.google.com/android/reference/com/google/android/gms/games/LeaderboardsClient).

## 후속 실기기 결과: pending

새 빌드에서 TenSum 8점의 전송 요청이 실제로 실행되는 것을 확인했다. 서버 Task의 실패 응답은 `ApiException: 26502: CLIENT_RECONNECT_REQUIRED`다. 반복 재시도에도 동일하다. 점수는 pendingScores.json에 유지돼 있다. 플레이어 ID는 이 문서에 기록하지 않는다.

설치된 APK를 다시 확인한 결과 SHA-1은 `23:F6:34:F9:31:C3:FB:81:4C:3F:90:46:A3:2B:4A:19:D6:1B:34:AE`로 이전 설치본과 같다. 단순 UI 갱신 지연이 아니라 점수 전송 Task가 실패하는 상황이다. 원인이 인증 정보 연결, 기존 연결 상태 등 중 무엇인지는 이 오류 코드만으로 확정하지 않았다.

다음 확인은 앱 완전 종료·재실행 후 보관된 점수의 재전송, 그리고 새 Android OAuth 클라이언트가 Cloud에 존재하는 것뿐 아니라 해당 PGS 게임의 Android 사용자 인증 정보에 연결됐는지 여부다. 현재 단계에서는 인증 상태를 임의로 초기화하거나 SDK 내부 상태를 강제 변경하는 코드를 추가하지 않았다.

공식 구성 문서는 Cloud의 OAuth 클라이언트 생성 후 Play Console에서 해당 클라이언트와 게임을 연결하는 별도 사용자 인증 정보가 필요하다고 설명한다: [PGS 구성](https://developer.android.com/games/pgs/console/setup).

## ?? ??

?? ?? ??? 10? ??, ?? 0?. ?? ? ?????? ? ????? ?? ? ?? ?? ??? ????. [?? ?? XML](ScoreUploadRegressionResults.xml). Android? C# ??? ??. Android ???? ?? ??? ??? ??? ? ???? ??? ???? ????.
