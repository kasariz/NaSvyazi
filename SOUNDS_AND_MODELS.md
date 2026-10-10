# Звуки и 3D-модели — что скачать и как подключить

Код уже умеет подхватывать внешние файлы: звуки — из `Resources/Sounds`, модели — командой
**На связи → 3D-модели → Подключить модели из Art/Models**. Всё, чего нет в папках, остаётся
как сейчас (синтезированный звук, модель из простых фигур), так что подключать можно по одному файлу.

Берите паки с лицензией **CC0** — их можно использовать в ВКР и в магазине приложений без указания автора.
С лицензией **CC-BY** тоже можно, но автора нужно указать (например, в разделе «Авторы» в меню).

## Что уже подключено (версия 0.3.0)

**Звуки** (`Assets/_Project/Resources/Sounds/`):

| Ключ | Файлы | Источник |
|---|---|---|
| scan | `scan.wav` | BigSoundBank — писк кассы (ID 1417) |
| ambience | `ambience.wav` | BigSoundBank — супермаркет (ID 0578), потоковая загрузка |
| notify | `notify.ogg` | Kenney Interface — bong_001 |
| purchase | `purchase_1…4` | Kenney Interface — confirmation |
| click | `click_1…5` | Kenney Interface — click |
| error | `error_1…4` | Kenney Interface — error_001…004 |
| scrub | `scrub_1…5` | Kenney Interface — scratch |
| step | `step_1…5` | Kenney Impact — footstep_concrete |
| pickup | `pickup_1…5` | Kenney Impact — impactSoft_medium |
| place | `place_1…5` | Kenney Impact — impactGeneric_light |
| doorbell | `doorbell_1…5` | Kenney Impact — impactBell_heavy |

Остальные звуки паков лежат в `Assets/_Project/Art/Audio/Library/` — они не попадают в сборку.
Чтобы заменить звук, переименуйте файл из библиотеки по ключу и переложите в `Resources/Sounds`.

**Модели** (`Assets/_Project/Art/Models/`):

| Куда | Файл | Источник |
|---|---|---|
| Box | `cardboardBoxClosed.fbx` | Kenney Furniture Kit |
| Furniture | `plant.fbx` (pottedPlant), `rug.fbx` (rugRectangle), `waiting_chairs.fbx` (loungeDesignSofa) | Kenney Furniture Kit |
| Products | `phone_basic.obj` (Smartphone), `phone_mid.obj` (1327 iPhone), `earbuds.obj` (Headphones), `speaker.obj` (Speaker) | Poly Pizza |

Остальные модели паков — в `Art/Models/Library/KenneyFurniture` и `Library/KenneyMiniMarket`
(команда подключения их не трогает). Чтобы использовать другую, скопируйте её в нужную папку
под именем по таблице ниже.

**Не хватает текстур:** `phone_basic.obj` ссылается на `endesga64_1024.png`, а `phone_mid.obj` — на
`1327 iPhone.png`. Их нет в репозитории, поэтому эти смартфоны будут белыми. Положите PNG из скачанных
архивов в `Art/Models/Products/` рядом с `.mtl` и запустите команду подключения ещё раз.

**Лицензии:** Kenney и BigSoundBank — CC0. У моделей с Poly Pizza проверьте лицензию на странице модели:
при CC-BY автора нужно указать в титрах.

---

## 1. Звуки

### Что скачать

| Пак | Лицензия | Что взять |
|---|---|---|
| [Kenney — Interface Sounds](https://kenney.nl/assets/interface-sounds) (100 звуков) | CC0 | клик, уведомление, ошибка |
| [Kenney — Impact Sounds](https://kenney.nl/assets/impact-sounds) (130 звуков) | CC0 | шаги по плитке/бетону, удары коробок, звук «поставил на полку» |
| [BigSoundBank — «Bip de caisse»](https://bigsoundbank.com/bip-de-caisse-s1417.html) | CC0 | писк сканера на кассе |
| [BigSoundBank — Supermarket](https://bigsoundbank.com/supermarket_s0578.html) | CC0 | фон зала (голоса, гул) |
| [BigSoundBank](https://bigsoundbank.com) — поиск «caisse enregistreuse», «sonnette», «serpillière» | CC0 | звон кассы, дверной колокольчик, швабра |

### Как установить

1. Создайте папку `Assets/_Project/Resources/Sounds/` (она уже есть в репозитории).
2. Переименуйте выбранные файлы по таблице и положите туда (`.ogg`, `.wav`, `.mp3` — любые):

| Имя файла | Где звучит |
|---|---|
| `scan` | сканирование товара на кассе |
| `purchase` | оплата, покупка в терминале, новый уровень |
| `notify` | всплывающее уведомление |
| `place` | товар поставлен на полку |
| `pickup` | взять / бросить коробку |
| `click` | кнопки интерфейса |
| `doorbell` | пришёл покупатель |
| `error` | ошибка, не хватает денег |
| `scrub` | швабра по полу |
| `step` | шаги героя |
| `music` | фоновая музыка (заменяет синтезированную) |
| `ambience` | фон зала поверх музыки (необязательно) |

3. Несколько вариантов одного звука — с номером: `step_1.ogg`, `step_2.ogg`, `step_3.ogg`.
   Каждый раз будет играть случайный — шаги и удары перестанут звучать механически.
4. Для `music` и `ambience` в инспекторе файла поставьте **Load Type: Streaming**, остальным — **Decompress On Load**.
5. Запустите игру — в консоли появится «[Звук] Загружено файлов-замен: N».

Громкость регулируется в настройках игры как раньше (музыка и фон — ползунком музыки).

---

## 2. 3D-модели

### Что скачать

| Пак | Лицензия | Что взять |
|---|---|---|
| [Kenney — Furniture Kit](https://kenney.nl/assets/furniture-kit) (140 моделей) | CC0 | растение в кашпо, кресла, ковёр, кулер/холодильник, техника |
| [Kenney — Mini Market](https://kenney.nl/assets/mini-market) (20 моделей) | CC0 | коробки, магазинный декор |
| [Quaternius — Ultimate Furniture](https://quaternius.com/packs/ultimatefurniture.html) | CC0 | стулья, столы |
| [Poly Pizza — поиск «phone», «headphones», «speaker»](https://poly.pizza/search/smartphone) | CC0 или CC-BY — смотрите на странице модели | смартфоны, наушники, колонка, зарядка |

Скачивайте формат **FBX** (или OBJ). GLB/GLTF Unity без дополнительного пакета не открывает.

### Как установить

1. Положите файлы в папки (имя файла = идентификатор):

**`Assets/_Project/Art/Models/Products/`** — товары:

| Файл | Товар |
|---|---|
| `phone_basic.fbx` | смартфон начального уровня |
| `phone_mid.fbx` | смартфон среднего уровня |
| `earbuds.fbx` | беспроводные наушники |
| `speaker.fbx` | портативная колонка |
| `charger.fbx` | зарядное устройство |
| `cable.fbx` | кабель USB-C |
| `case.fbx` | чехол |
| `glass.fbx` | защитное стекло |
| `sim.fbx` | стартовый пакет (SIM) |

**`Assets/_Project/Art/Models/Furniture/`** — декор:
`plant.fbx`, `water_cooler.fbx`, `waiting_chairs.fbx`, `rug.fbx`, `aquarium.fbx`,
`ad_stand.fbx`, `neon_sign.fbx`, `security_camera.fbx`.

**`Assets/_Project/Art/Models/Box/`** — одна модель картонной коробки (имя любое).

2. **На связи → 3D-модели → Подключить модели из Art/Models.** Команда:
   - вписывает модель в габариты прежней (товар — в размер из карточки товара, мебель — в размер простой модели);
   - ставит её на пол и убирает её коллайдеры — столкновения, полки, подсветка и надписи остаются прежними,
     поэтому ничего в механике не ломается;
   - на коробке оставляет цветную полосу товара — коробки по-прежнему различимы издалека.
3. Поворот подбирается автоматически (лежащий смартфон ставится на ребро). Если модель всё же стоит
   задом наперёд или боком — задайте поворот в имени файла и запустите команду ещё раз:
   `phone_basic@180.obj` (вокруг вертикали), `speaker@x-90.obj`, `phone_mid@x-90y180.obj`.
4. Не понравилось — **На связи → 3D-модели → Вернуть простые модели** (файлы моделей не удаляются).

**Ограничение:** стеллажи, витрина, стойка у кассы и складской стеллаж не заменяются — места товаров
на их полках рассчитаны под текущую геометрию. Их можно перекрасить или заменить позже отдельной итерацией.

### Если модель розовая

Материал не перевёлся на URP. Откройте **Window → Rendering → Render Pipeline Converter**, выберите
**Built-in to URP → Material Upgrade** и нажмите **Convert Assets**. Либо в Inspector модели: вкладка
**Materials → Extract Materials**, затем смените шейдер материалов на *Universal Render Pipeline/Lit*.
