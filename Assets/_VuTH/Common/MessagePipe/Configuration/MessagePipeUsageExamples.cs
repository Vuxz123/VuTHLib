using System;
using MessagePipe;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace VuTH.Common.MessagePipe
{
    /// <summary>
    /// Usage examples for MessagePipe event system.
    /// See also: https://github.com/Cysharp/MessagePipe
    ///
    /// IMPORTANT: After adding or modifying [MessagePipeEvent] attributes, you must
    /// re-bake the event scope lookup by running: VuTH/MessagePipe/Bake Event Scope Lookup
    /// This generates MessagePipeRegistrar.cs for optimized runtime registration.
    /// Without re-baking, fallback lookup-based registration will be used.
    /// </summary>
    public static class MessagePipeUsageExamples
    {
        // Example 1: Global Event
        // ============================================

        // Define event type (can be class or struct)
        public sealed class OnPlayerDeathEvent
        {
            public int PlayerId;
            public Vector3 Position;
            public float Damage;
        }

        // Subscribe to global event (e.g., in a MonoBehaviour)
        // public void SubscribeExample(ISubscriber<OnPlayerDeathEvent> subscriber)
        // {
        //     subscriber.Subscribe(OnPlayerDeath);
        // }
        //
        // private void OnPlayerDeath(OnPlayerDeathEvent deathEvent)
        // {
        //     Debug.Log($"Player {deathEvent.PlayerId} died at {deathEvent.Position}");
        // }

        // Publish global event
        // public void PublishExample(IPublisher<OnPlayerDeathEvent> publisher)
        // {
        //     publisher.Publish(new OnPlayerDeathEvent
        //     {
        //         PlayerId = 123,
        //         Position = Vector3.zero,
        //         Damage = 100f
        //     });
        // }

        // ============================================
        // Example 2: Scene-Scoped Event
        // ============================================

        // Define event type with [MessagePipeEvent] attribute
        // IMPORTANT: For Scene scope, you MUST specify the SceneName matching the Unity scene name.
        // Events with mismatched or missing SceneName will be skipped at runtime with a warning.
        // [MessagePipeEvent(EventScope.Scene, "MySceneName")]
        // public sealed class OnDialogClosedEvent
        // {
        //     public string DialogId;
        //     public bool Confirmed;
        // }

        // Subscribe to scene-scoped event (same as global)
        // public void SubscribeSceneExample(ISubscriber<OnDialogClosedEvent> subscriber)
        // {
        //     subscriber.Subscribe(OnDialogClosed);
        // }

        // ============================================
        // Example 3: Using MessagePipeHelper (non-VContainer mode)
        // ============================================

        // At startup, initialize the global broker:
        // MessagePipeHelper.InitializeGlobalBroker();

        // To publish:
        // var publisher = MessagePipeHelper.GetPublisher<OnPlayerDeathEvent>();
        // publisher.Publish(new OnPlayerDeathEvent { ... });

        // To subscribe:
        // var subscriber = MessagePipeHelper.GetSubscriber<OnPlayerDeathEvent>();
        // subscriber.Subscribe(OnPlayerDeath);

        // ============================================
        // Example 4: VContainer Integration
        // ============================================

        // In RootScopeContainer:
        // public void Configure(IContainerBuilder builder)
        // {
        //     MessagePipeHelper.RegisterGlobalEvents(builder);
        // }

        // In SceneScopeContainer:
        // public void Configure(IContainerBuilder builder)
        // {
        //     MessagePipeHelper.RegisterSceneEvents(builder);
        // }

        // ============================================
        // Example 5: Async Disposable
        // ============================================

        // Use async/await with SubscribeAsync for proper cleanup:
        // var disposable = subscriber.SubscribeAsync<OnPlayerDeathEvent>(async (deathEvent, ct) =>
        // {
        //     await ProcessDeathAsync(deathEvent, ct);
        // });
        //
        // // Dispose when done
        // await disposable.DisposeAsync();
    }
}
