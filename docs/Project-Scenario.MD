## Team 16 트리오링고
|항목|내용|
|---|---|
|프로젝트명|플레이어가 대화, 몸짓을 활용해 특정 NPC를 훈련시키고, 그 NPC를 마을의 수호자로 성장시키는 스토리형 어드벤처 게임|
|프로젝트 키워드|멀티모달 AI, NPC와 다양한 상호작용, 성장 스토리 어드벤처 게임|
|트랙| 산학 |
|프로젝트멤버| 강한나, 박세은, 최은별|
|팀지도교수|김영준|
|무엇을 만들고자 하는가|**‘GoGo Golem!‘** 게임은 아기자기하고 따스한 판타지 세계를 무대로, 대대로 마을을 수호하는 완벽한 골렘을 만들어야 하는 장인 가문의 후계자인 플레이어와 그 손에서 태어난 예상 밖의 골렘이 함께 성장하는 스토리 어드벤처입니다. 마을의 일반적인 골렘과 다르게 복종하지 않고 질문하는 호기심 많은 골렘과 음성, 손짓 등 다양한 방식으로 상호작용하며 각별한 사이가 됩니다.|
|고객|&nbsp;&nbsp;&nbsp;&nbsp;박세운 씨는 AI 스타트업에서 근무하는 30대 초반의 직장인이다. 매일 아침 출근길에 뉴스레터를 읽을 정도로 **AI 시장과 신기술에 깊은 관심**을 가지고 있다. 새롭게 등장한 AI 제품이나 도구가 있으면 누구보다 먼저 직접 사용해보는 얼리어답터 성향을 보인다. 특히 지금까지 시장에서 접하지 못한 새로운 도전과 방향성을 가진 제품에 이끌리며 고가의 하드웨어 없이 일반 노트북만으로도 쉽게 접근 가능한 서비스를 선호한다.<br><br>&nbsp;&nbsp;&nbsp;&nbsp;강한아 씨는 미디어 애호가인 20대 중반의 대학생이다. 책·영화·전시·게임 등 장르를 가리지 않고 다양한 미디어와 콘텐츠를 소비한다. 특히 **판타지와 SF 작품을 선호**하며, 세계관에 몰입하여 마치 자신이 작품 속 인물이 되는 상상을 즐겨 한다. 때문에 게임의 경우 마치 게임 속 인물들과 함께 여정을 나누고 성장하는 듯한 스토리형 작품을 좋아한다.<br><br>&nbsp;&nbsp;&nbsp;&nbsp;최금별 씨는 10대 후반 고등학생이다. 평소 가게에서 발견한 작고 귀여운 잡동사니를 수집하는 것을 좋아하고 모바일 및 PC 게임을 즐겨 한다. 특히 게임에서 큰 성취를 이루기보다 **NPC와 애착관계를 가지고 상호작용하는 평화로운 게임**에 열광하며, 아기자기하고 따뜻한 그래픽과 작고 귀여운 캐릭터가 등장하는 게임을 선호한다.|
|Vitamin|**신체 동작과 게임 동작의 일치성 강화**<br>플레이어의 실제 신체 움직임을 게임 내 동작으로 즉각 전환하는 모션 인식 상호작용을 통해 조작의 직관성을 높인다. 플레이어의 물리적인 의도와 게임 속 동작 사이의 간극을 좁힘으로써, 게임 시스템을 본능적으로 통제하고 있다는 <strong>조작적 몰입(게임 시스템을 이해하고 통제하는 과정에서 느끼는 몰입)</strong>을 극대화한다.<br><br>**음성 및 텍스트 기반의 심화된 상호작용**<br>NPC와 음성으로 직접 대화하며 논의하거나, 텍스트를 통해 서신을 왕래하는 등 능동적인 소통 방식을 도입한다. 이러한 양방향 상호작용은 캐릭터를 정해진 패턴을 반복하는 기능적 존재가 아닌 유기적인 인격체로 인식하게 하며, 플레이어가 게임의 세계관과 관계망에 깊이 동화되는 <strong>서사적 몰입(게임의 세계관, 스토리, 캐릭터에 빠져드는 몰입)</strong>을 제공한다.|
|사용할 소프트웨어 패키지의 명칭과 핵심기능/용도, 사용시나리오|**Multimodal**: 플레이어의 다양한 인풋 데이터 수집 및 학습<br>&nbsp;&nbsp;• **MediaPipe Unity Plugin**: Real Time Motion Tracking으로 플레이어 제스처를 감지하고 게임 동작과 연동<br>&nbsp;&nbsp;• **Whisper**: 음성을 인식하고 플레이어의 음성을 텍스트로 변환<br><br>**Unity**: 게임 엔진<br>&nbsp;&nbsp;• **Unity NativeWebSocket**: WebSocket을 이용한 유니티 <> AI 서버 백엔드 실시간 통신<br>&nbsp;&nbsp;• **Unity Firebase SDK**: Firebase와 Unity 연동<br><br>**database**: 게임 컨텐츠 데이터 저장<br>&nbsp;&nbsp;• **Firebase**: AI 서버에서 동적으로 생성된 게임 콘텐츠를 저장하고 Unity 클라이언트에서 불러오는 구조|
|사용할 소프트웨어 패키지의 명칭과 URL|MediaPipe Unity Plugin https://github.com/homuler/MediaPipeUnityPlugin<br>Whisper https://github.com/openai/whisper<br>Unity WebSocket https://github.com/endel/NativeWebSocket<br>Unity Firebase SDK https://github.com/firebase/firebase-unity-sdk<br>FastAPI https://fastapi.tiangolo.com/<br>Firebase https://firebase.google.com/docs|
|팀그라운드룰|https://github.com/ob1hnk/Triolingo/blob/main/docs/GroundRules.MD|
|최종수정일|2025.12.18|