# 🧠 تقرير تفصيلي: كيف يعمل الـ Chat والـ Orchestrator في WanderAI

> **التاريخ:** يونيو 2026  
> **المشروع:** WanderAI - Smart Travel Planner  
> **المسار:** `SmartTravelPlaners.BLL`

---

## 📋 فهرس المحتويات

1. [نظرة عامة على المعمارية](#-نظرة-عامة-على-المعمارية)
2. [كيف يشتغل الـ Chat](#-كيف-يشتغل-الـ-chat)
3. [كيف يشتغل الـ Orchestrator](#-كيف-يشتغل-الـ-orchestrator)
4. [رحلة المستخدم من أول لآخر كلمة](#-رحلة-المستخدم-من-أول-لآخر-كلمة)
5. [توزيع الميزانية (BudgetAllocator)](#-توزيع-الميزانية-budgetallocator)
6. [نقاط الضعف الموجودة دلوقتي](#-نقاط-الضعف-الموجودة-دلوقتي)
7. [أفكار لتحسين الذكاء](#-أفكار-لتحسين-الذكاء)

---

## 🏗 نظرة عامة على المعمارية

النظام بيعمل بطريقة **Layered Architecture** مقسم على 3 طبقات:

```
PL (Presentation Layer)
   ChatController / OrchestratorTestController
          |
BLL (Business Logic Layer)
   ChatService <---> TripOrchestratorService
       |                    |
   BudgetAllocator     Plugins: Hotel / Flight / Places / Weather
          |
DAL (Data Access Layer)
   Repositories / UnitOfWork / EF Core
```

**المكونات الأساسية:**

| المكون | الملف | الدور |
|--------|-------|-------|
| `ChatService` | `Features/Chat/Services/ChatService.cs` | واجهة المحادثة مع AI |
| `TripOrchestratorService` | `Features/Orchestrator/Services/TripOrchestratorService.cs` | بناء خطة الرحلة كاملة |
| `BudgetAllocator` | `Features/Orchestrator/Services/BudgetAllocator.cs` | توزيع الميزانية |
| `ChatController` | `PL/Controllers/ChatController.cs` | REST API endpoints |

---

## 💬 كيف يشتغل الـ Chat

### الفكرة الأساسية

الـ `ChatService` هو اللي بيتكلم مع المستخدم. بيستخدم **Microsoft Semantic Kernel** للتواصل مع نموذج AI (GitHub Models). الـ AI مش بيرد بكلام عادي بس، لكن بيستخدم **بروتوكول خاص** عبارة عن كلمات مفتاحية (Keywords) عشان يقول للنظام "إيه اللي المستخدم عاوزه".

### System Prompt: قلب الموضوع كله

في أول كل محادثة، بيتبعت للـ AI رسالة نظام (System Prompt) بتقوله:

```
أنت مساعد سفر اسمك TravelBot.
- تكلم المستخدم بالعربي بأسلوب ودي.
- مهمتك: تجمع بيانات السفر أو تعدل رحلة موجودة.
```

والـ AI بيتدرب على إنه لما يكون جمع كل البيانات، **مايردش بكلام** - بيرد بـ **keyword + JSON** بالشكل ده:

```
TRIP_READY:{"destination":"Paris","originCity":"Cairo","startDate":"2025-01-15",...}
```

### الـ Keywords اللي الـ AI بيستخدمها

| الكلمة المفتاحية | المعنى | مثال |
|------------------|--------|-------|
| `TRIP_READY:{...}` | كل البيانات اتجمعت، هيبني رحلة جديدة | لما المستخدم يحدد الوجهة والتواريخ والميزانية |
| `TRIP_SHOW:{}` | المستخدم عاوز يشوف تفاصيل رحلته | "ابعت لي الرحلة" |
| `TRIP_UPDATE_HOTEL:{}` | المستخدم مش عاجبه الفندق | "غيرلي الفندق" |
| `TRIP_UPDATE_FLIGHT:{}` | المستخدم عاوز رحلة طيران تانية | "غيرلي الطيران" |
| `TRIP_UPDATE_ACTIVITIES:{dayNumber:2}` | المستخدم عاوز أنشطة تانية ليوم معين | "عايز أنشطة تانية يوم 3" |
| `TRIP_UPDATE_FIELD:{field:"destination", value:"Dubai"}` | تغيير حقل واحد في الرحلة | "غير وجهتي لدبي" |
| `TRIP_UPDATE:{...}` | نفس الفوق (نسخة تانية من نفس الـ keyword) | نفس الغرض |

### خطوات SendMessageAsync بالتفصيل

```
المستخدم يبعت رسالة
       |
1. جيب الـ Session من DB
2. تحقق إن المستخدم هو صاحب الـ Session
3. تحقق من حد الرسائل (Usage Limit)
       |
4. ابني ChatHistory:
   - System Prompt (التعليمات للـ AI)
   - لو في رحلة موجودة -> system message إضافي
   - كل الرسايل السابقة (User + Assistant)
   - الرسالة الجديدة
       |
5. ابعت الـ History للـ AI (GitHub Models) بـ timeout 15 ثانية
       |
6. تحليل الرد:
   - لو في "TRIP_READY:"         -> إنشاء رحلة جديدة
   - لو في "TRIP_SHOW:"          -> عرض الرحلة الحالية
   - لو في "TRIP_UPDATE_HOTEL:"  -> تغيير الفندق
   - لو في "TRIP_UPDATE_FLIGHT:" -> تغيير الطيران
   - لو في "TRIP_UPDATE_ACTIVITIES:" -> تغيير أنشطة يوم
   - لو في "TRIP_UPDATE_FIELD:"  -> تعديل حقل
   - غير كده -> رد عادي من الـ AI
       |
7. احفظ الرسالة في DB
8. زود عداد الاستخدام
9. ارجع الـ Reply للمستخدم
```

### الـ Background Tasks (المهام الخلفية)

لما المستخدم يعدل الفندق أو الطيران أو الأنشطة، النظام **بيبعت الطلب في background** باستخدام `Task.Run` عشان المستخدم مش هيستنى - بيرد فورًا بـ "جاري التغيير..." وبيشتغل في الخلفية.

```csharp
// مثال: تغيير الفندق في الخلفية
_ = Task.Run(async () => {
    using var scope = _serviceProvider.CreateScope();
    var orchestrator = scope.ServiceProvider.GetRequiredService<ITripOrchestratorService>();
    await orchestrator.RegenerateHotelAsync(tripId);
});
finalReply = "جاري البحث عن فندق بديل... ثواني وهنعرضهولك.";
```

### Cascade Updates (التحديثات المتسلسلة)

لما المستخدم يغير **وجهة الرحلة** مثلاً، النظام بيعمل cascade update:

```
تغيير الوجهة     -> يغير الفندق + الطيران + كل الأنشطة
تغيير التاريخ    -> يغير الفندق + الطيران + يزامن الأيام
تغيير المسافرين  -> يغير الفندق بس
تغيير الميزانية  -> يغير الفندق + كل الأنشطة
تغيير مدينة المغادرة -> يغير الطيران بس
```

---

## 🎭 كيف يشتغل الـ Orchestrator

### الفكرة الأساسية

`TripOrchestratorService` مش بيتكلم مع المستخدم خالص - ده الـ **"المنسق"** اللي بياخد بيانات الرحلة ويبني الخطة كاملة بالتنسيق مع كل الـ Plugins.

### BuildTripPlanAsync: بناء الخطة من الصفر

```
1. جيب بيانات الرحلة من DB
2. احسب الميزانية (BudgetAllocator):
   - لو في مدينة مغادرة: 40% فندق + 35% طيران + 25% أنشطة
   - لو من غير مدينة مغادرة: 61% فندق + 39% أنشطة
       |
3. SelectHotelAsync:
   - ابحث بـ FilterHotelsAsync (max price + min rating 3.5)
   - لو مفيش نتائج -> ابحث بـ SearchHotelsAsync
   - اختار أعلى تقييم في الميزانية
       |
4. SelectFlightAsync (لو في مدينة مغادرة):
   - ابحث بـ FlightPlugin.SearchFlightsAsync
   - اختار أول نتيجة
       |
5. في نفس الوقت (Parallel):
   - GetWeatherAsync      -> جيب توقعات الطقس
   - BuildDayPlansAsync   -> ابني خطة كل يوم
       |
6. ربط الطقس بكل يوم
       |
7. PersistPlanAsync: احفظ كل حاجة في DB
       |
8. ارجع TripPlanDto كامل
```

### BuildDayPlansAsync: خطة كل يوم

```
1. احسب الميزانية اليومية = ميزانية الأنشطة / عدد الأيام
2. ابعت طلبات بحث موازية (Parallel) لكل فئة:
   [attraction, restaurant, cafe, museum, park, shopping + preferences]
3. لكل يوم:
   - اختار عشوائي 2-4 نشاط
   - وزع التايم سلوتس بشكل عشوائي (Morning/Afternoon/Evening/Night)
   - اختار أماكن من كل فئة بدون تكرار
4. ارجع قائمة الأيام مرتبة
```

### RegenerateHotelAsync: تغيير الفندق

```
1. جيب الرحلة من DB
2. خزن اسم الفندق الحالي
3. ابحث عن فنادق بديلة (نفس المنطقة + نفس الميزانية)
4. استبعد الفندق الحالي من النتائج
5. اختار أعلى تقييم متاح
6. حدّث الـ Entity في DB
```

### RegenerateFlightAsync: تغيير الطيران

```
1. جيب الرحلة من DB
2. لو مفيش مدينة مغادرة -> ارجع null
3. ابحث عن رحلات بديلة (نفس المسار + نفس التاريخ)
4. استبعد رقم الرحلة الحالي
5. اختار أول نتيجة مختلفة
6. حدّث الـ Entity في DB
```

### RegenerateDayActivitiesAsync: تغيير أنشطة يوم

```
1. جيب TripDay من DB بالـ dayNumber
2. احسب الميزانية اليومية
3. احذف كل الأنشطة القديمة من DB
4. ابحث عن أماكن جديدة (نفس طريقة BuildDayPlansAsync)
5. أضف الأنشطة الجديدة في DB
```

### SyncDayPlansAsync: مزامنة الأيام عند تغيير التواريخ

```
لو عدد الأيام اتغير:
- لو زاد:    أضف أيام جديدة بأنشطة
- لو نقص:   احذف الأيام الزيادة وأنشطتها
- لو نفس العدد: حدّث التواريخ بس
```

### GetCurrentPlanAsync: عرض الخطة الحالية

```
جيب من DB:
- بيانات الرحلة
- الفندق
- الطيران
- الأيام والأنشطة
- الطقس
-> ارجع TripPlanDto كامل
```

### ClearExistingPlanAsync: مسح الخطة القديمة

قبل أي rebuild، الأوركستريتور بيمسح كل حاجة عشان يضمن مفيش duplicates:

```
احذف: الأنشطة -> الأيام -> الفنادق -> الطيران -> الطقس
-> حفظ في DB
-> مسح الـ navigation properties من الـ in-memory object
```

---

## 🔄 رحلة المستخدم من أول لآخر كلمة

```
المستخدم: "عاوز أسافر باريس من القاهرة يناير 2025, 15 يوم, ميزانية 5000 دولار"
       |
ChatController - POST /api/chat/send
   - استخرج userId من الـ JWT Token
       |
ChatService
   1. تحقق من الـ Session والصلاحية
   2. تحقق من حد الرسائل الشهري
   3. ابني ChatHistory مع System Prompt
   4. ابعت للـ AI (GitHub Models / Semantic Kernel)
       |
GitHub Models AI
   رد: TRIP_READY:{"destination":"Paris","originCity":"Cairo",
       "startDate":"2025-01-15","endDate":"2025-01-30",
       "numTravelers":1,"budgetTotal":5000,"preferences":[]}
       |
ChatService - TRIP_READY Handler
   1. Parse الـ JSON
   2. TripCreationService.CreateAndBuildAsync()
      - أنشئ Trip في DB
      - TripOrchestratorService.BuildTripPlanAsync()
        + BudgetAllocator (40/35/25)
        + HotelPlugin.FilterHotelsAsync()
        + FlightPlugin.SearchFlightsAsync()
        + WeatherPlugin.GetWeatherTimeline() [Parallel]
        + PlacesPlugin.SearchWithImages() x6 [Parallel]
        + PersistPlanAsync() -> حفظ كل حاجة في DB
       |
الرد للمستخدم:
   "ممتاز! جاري تجهيز أفضل خطة لرحلتك إلى Paris (من 2025-01-15 إلى 2025-01-30). ثواني وهتكون جاهزة"
```

---

## 💰 توزيع الميزانية (BudgetAllocator)

### مع مدينة مغادرة (hasOrigin = true)

```
ميزانية كلية: 5000$
|-- فندق:    40% = 2000$ (133$/ليلة x 15 ليلة)
|-- طيران:   35% = 1750$
|-- أنشطة:  25% = 1250$ (83$/يوم x 15 يوم)
```

### بدون مدينة مغادرة (hasOrigin = false)

```
ميزانية كلية: 5000$
|-- فندق:    61% = 3050$  (الـ 35% الطيران بتتوزع: 60% فندق + 40% أنشطة)
|-- أنشطة:  39% = 1950$
```

---

## ⚠️ نقاط الضعف الموجودة دلوقتي

### 1. الـ System Prompt محدود التعليمات

الـ AI مش عارف حاجات زي:
- الميزانية المعقولة لكل وجهة
- الأنشطة المناسبة حسب التفضيلات
- سياق محادثات سابقة للمستخدم
- ما إذا كانت الميزانية واقعية للوجهة المطلوبة

### 2. اختيار الأماكن عشوائي بالكامل

```csharp
// الكود الحالي
var place = available[Random.Shared.Next(available.Count)];
```

النظام بياخد أماكن عشوائية من Foursquare - مش بيراعي:
- تقييم المكان
- السعر
- المسافة بين الأماكن في اليوم الواحد
- تفضيلات المستخدم الفعلية

### 3. مفيش Context للمستخدم

النظام مش حافظ أي حاجة عن المستخدم:
- رحلاته السابقة
- تفضيلاته المتكررة
- المدن اللي زارها
- الفنادق اللي عجبته

### 4. الـ Task.Run بدون Tracking

```csharp
_ = Task.Run(async () => { ... });
// المستخدم مش عارف لما الـ task خلصت
```

المستخدم مش عارف لما الرحلة اتبنت فعلاً. لازم يـ refresh أو يسأل.

### 5. نموذج لغوي واحد لكل حاجة

الـ AI المستخدم دلوقتي بيعمل كل حاجة: فهم القصد (intent), جمع البيانات, واتخاذ القرار. ده ممكن يسبب أخطاء.

---

## 💡 أفكار لتحسين الذكاء

### الفكرة الأولى: تحسين الـ System Prompt بـ Context ذكي

**الوضع الحالي:** الـ System Prompt ثابت لكل المستخدمين.

**الفكرة:** إضافة context ديناميكي للـ System Prompt:

```csharp
// أضف للـ System Prompt بيانات المستخدم
var userProfile = await _userProfileRepo.GetByUserIdAsync(userId);
var pastTrips = await _tripRepo.GetRecentTripsAsync(userId, limit: 3);

var contextualPrompt = $@"
معلومات عن المستخدم:
- رحلاته السابقة: {string.Join(", ", pastTrips.Select(t => t.Destination))}
- تفضيلاته: {string.Join(", ", userProfile.Preferences)}

لو المستخدم طلب ميزانية منخفضة لوجهة غالية، نبهه بشكل ودي.
";

history.AddSystemMessage(contextualPrompt);
```

**الكود اللي تضيفه في ChatService.cs:**

```csharp
// قبل history.AddUserMessage(userMessage);
var profile = await _userProfileRepo.GetByUserIdAsync(session.UserId);
if (profile != null)
{
    history.AddSystemMessage(
        $"معلومات المستخدم - التفضيلات: {string.Join(", ", profile.Preferences ?? new())}");
}
```

---

### الفكرة الثانية: نظام Intent Detection منفصل

**الوضع الحالي:** نموذج واحد بيفهم + بيجمع البيانات + بيقرر.

**الفكرة:** خلي نموذج أول خفيف (أسرع وأرخص) يحدد **القصد** بس:

```csharp
public enum TravelIntent
{
    CollectingInfo,      // لسه بيسأل
    CreateTrip,          // جاهز ينشئ رحلة
    ModifyHotel,         // عاوز يغير فندق
    ModifyFlight,        // عاوز يغير طيران
    ModifyActivities,    // عاوز يغير أنشطة
    ShowTrip,            // عاوز يشوف رحلته
    GeneralQuestion      // سؤال عام
}
```

**الإيجابيات:** سرعة أكبر + دقة أعلى في فهم قصد المستخدم.

---

### الفكرة الثالثة: اختيار الأماكن بـ Scoring ذكي

**الوضع الحالي:** اختيار عشوائي من قائمة الأماكن.

**الفكرة:** نظام تسجيل نقاط لكل مكان:

```csharp
private double ScorePlace(PlaceDto place, decimal costPerActivity, List<string> userPreferences)
{
    double score = 0;

    // Rating مرجح بـ 40%
    score += (place.Rating ?? 0) * 0.4;

    // مطابقة التفضيلات مرجحة بـ 30%
    if (userPreferences.Any(p => place.Category?.Contains(p) == true))
        score += 3.0 * 0.3;

    // ميزانية متوازنة مرجحة بـ 30%
    var priceDiff = Math.Abs((double)(place.EstimatedCost - costPerActivity));
    score += Math.Max(0, 3 - priceDiff / 50) * 0.3;

    return score;
}

// بدل العشوائي:
var place = available.OrderByDescending(p => ScorePlace(p, costPerActivity, userPrefs)).First();
```

---

### الفكرة الرابعة: SignalR للتحديث الفوري

**الوضع الحالي:** المستخدم مش عارف لما الرحلة خلصت في الخلفية.

**الفكرة:** استخدام **SignalR** للإشعارات الفورية:

```csharp
// في TripOrchestratorService بعد ما يخلص
await _hubContext.Clients.User(userId)
    .SendAsync("TripReady", new { TripId = tripId, Message = "رحلتك جاهزة! 🎉" });
```

**في الـ Frontend (Angular):**
```typescript
this.hubConnection.on('TripReady', (data) => {
    this.loadTripPlan(data.TripId);
    this.showNotification('رحلتك جاهزة! 🎉');
});
```

---

### الفكرة الخامسة: Budget Validation ذكي

**الوضع الحالي:** النظام بياخد الميزانية زي ما هي حتى لو مش واقعية.

**الفكرة:** إضافة validation في الـ Chat قبل إنشاء الرحلة:

```csharp
// في ChatService - بعد TRIP_READY
var minBudget = EstimateMinimumBudget(dto.Destination, dto.NumTravelers, dto.StartDate, dto.EndDate);
if (dto.BudgetTotal < minBudget)
{
    finalReply = $"الميزانية {dto.BudgetTotal}$ قد تكون قليلة لرحلة لـ {dto.Destination} " +
                 $"لـ {days} أيام. الحد الأدنى المقترح هو {minBudget}$. " +
                 "هل تريد المتابعة أو تعديل الميزانية؟";
    return new ChatReplyDto { Message = finalReply };
}
```

---

### الفكرة السادسة: Preference Learning تلقائي

**الوضع الحالي:** التفضيلات بتتحدد مرة في الـ Profile.

**الفكرة:** تحليل سلوك المستخدم وتحديث التفضيلات تلقائياً:

```csharp
// لما المستخدم يغير الفندق أكتر من مرة
// -> الجهاز يتعلم إنه بيفضل فنادق بتقييم أعلى

// لما المستخدم بيختار أنشطة معينة أكتر
// -> الجهاز يقدمها أول في المرات الجاية

public async Task UpdateLearnedPreferencesAsync(string userId, string interactionType)
{
    var profile = await _userProfileRepo.GetByUserIdAsync(userId);
    switch (interactionType)
    {
        case "hotel_changed":
            profile.PreferHighRatingHotels = true;
            break;
        case "museum_selected":
            profile.IncrementCategoryScore("museum");
            break;
    }
    await _unitOfWork.CompleteAsync();
}
```

---

### الفكرة السابعة: Multi-turn Conversation Memory

**الوضع الحالي:** الـ AI بيشوف كل تاريخ المحادثة كل مرة (تقيل + غالي).

**الفكرة:** تلخيص المحادثات القديمة:

```csharp
// لو عدد الرسايل > 20
// -> لخصهم في رسالة واحدة بالـ AI
// -> استخدم الملخص بدل الرسايل القديمة

private async Task<string> SummarizeOldMessagesAsync(List<ChatMessage> oldMessages)
{
    var summaryHistory = new ChatHistory();
    summaryHistory.AddSystemMessage(
        "لخص المحادثة دي في جملة واحدة بالعربي تشمل: الوجهة والتواريخ والميزانية.");
    summaryHistory.AddUserMessage(
        string.Join("\n", oldMessages.Select(m => $"{m.Role}: {m.Content}")));
    var result = await _ai.GetChatMessageContentAsync(summaryHistory);
    return result?.Content ?? "";
}
```

---

### الفكرة الثامنة: Parallel Plan Building أذكى

**الوضع الحالي:** Hotel -> Flight -> (Weather ‖ Days) بالترتيب ده.

**الفكرة:** عمل أكبر قدر من الطلبات في parallel:

```csharp
// شغل الكل بالتوازي
var hotelTask   = SelectHotelAsync(trip, checkIn, checkOut, hotelBudget, numberOfNights);
var flightTask  = hasOrigin ? SelectFlightAsync(trip, checkIn) : Task.FromResult<FlightDto?>(null);
var weatherTask = GetWeatherAsync(trip.Destination, trip.StartDate, trip.EndDate);
var placesTask  = PreloadAllPlacesAsync(trip.Destination, categories);

await Task.WhenAll(hotelTask, flightTask, weatherTask, placesTask);
// دي هتوفر 30-50% من وقت الاستجابة!
```

---

## 📊 ملخص التحسينات المقترحة

| التحسين | الصعوبة | التأثير | الأولوية |
|---------|---------|---------|---------|
| تحسين System Prompt بـ Context | سهل | عالي | فوري |
| Budget Validation ذكي | سهل | متوسط | فوري |
| Parallel Plan Building | سهل | عالي (30-50% أسرع) | فوري |
| Scoring ذكي للأماكن | متوسط | عالي | قريب |
| SignalR للتحديث الفوري | صعب | عالي | قريب |
| Intent Detection منفصل | صعب | متوسط | مستقبلي |
| Preference Learning | صعب | عالي | مستقبلي |
| Conversation Summary | متوسط | متوسط | مستقبلي |

---

## 🏁 خلاصة

النظام الحالي شغال بطريقة **Keyword-Based AI Orchestration** - الـ AI مش بيتخذ قرارات مباشرة، لكن بيرد بـ keywords محددة، والكود بيتعامل معاهم. ده نظام **موثوق وسهل التحكم فيه** بس محدود في الذكاء.

**أسهل 3 خطوات تبدأ بيها دلوقتي:**

1. ✅ **أضف Context للـ System Prompt** (1-2 ساعة شغل)
2. ✅ **اعمل Parallel Building للـ Plan** (ساعة شغل، هتوفر 30-50% من وقت الاستجابة)
3. ✅ **أضف Budget Validation** (2-3 ساعات شغل، هتحسن تجربة المستخدم كتير)

---

*تقرير معمول بواسطة: Antigravity AI Assistant*  
*المرجع: الكود المصدري في `SmartTravelPlaners.BLL/Features/Chat` و `SmartTravelPlaners.BLL/Features/Orchestrator`*
