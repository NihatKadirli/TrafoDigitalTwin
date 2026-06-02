#!/usr/bin/env python3
import time
from datetime import datetime

import paho.mqtt.client as mqtt


BROKER_HOST = "localhost"
BROKER_PORT = 1883

MESSAGES_START = [
    ("substation/attack/type", "oil_critical_alarm"),
    ("substation/transformer/oil_temperature", "105"),
    ("substation/transformer/oil_level", "22"),
    ("substation/protection/buchholz", "WARNING"),
    ("substation/transformer/oil_alarm", "ON"),
]

MESSAGES_STOP = [
    ("substation/transformer/oil_temperature", "55"),
    ("substation/transformer/oil_level", "78"),
    ("substation/protection/buchholz", "CLEAR"),
    ("substation/transformer/oil_alarm", "OFF"),
    ("substation/effect/smoke", "OFF"),
    ("substation/attack/type", "none"),
]


def print_menu() -> None:
    print()
    print("Transformer Oil Critical Alarm Scenario")
    print("1 - Start Oil Temperature / Buchholz Alarm")
    print("2 - Stop Scenario / Restore Normal")
    print("q - Quit")


def publish_messages(messages: list[tuple[str, str]]) -> None:
    client = mqtt.Client(
        callback_api_version=mqtt.CallbackAPIVersion.VERSION2,
        client_id="oil-critical-alarm-console",
    )

    print(f"Connecting to MQTT broker {BROKER_HOST}:{BROKER_PORT}...")
    client.connect(BROKER_HOST, BROKER_PORT, keepalive=60)
    client.loop_start()

    timestamp = datetime.now().isoformat(timespec="seconds")
    for topic, payload in messages:
        result = client.publish(topic, payload, qos=0)
        result.wait_for_publish()
        print(f"[{timestamp}] Published {topic} = {payload}")
        time.sleep(0.05)

    client.loop_stop()
    client.disconnect()


def start_attack() -> None:
    print("[Oil Critical Alarm] Starting scenario...")
    publish_messages(MESSAGES_START)
    print("[Oil Critical Alarm] Attack messages sent.")


def stop_attack() -> None:
    print("[Oil Critical Alarm] Restoring normal state...")
    publish_messages(MESSAGES_STOP)
    print("[Oil Critical Alarm] Restore messages sent.")


def main() -> None:
    while True:
        print_menu()
        choice = input("Select command type: ").strip().lower()

        if choice in {"q", "quit", "exit"}:
            print("Exiting oil critical alarm console.")
            break

        try:
            if choice == "1":
                start_attack()
            elif choice == "2":
                stop_attack()
            else:
                print("Invalid selection. Use 1, 2, or q.")
        except Exception as exc:
            print(f"Publish failed: {exc}")


if __name__ == "__main__":
    main()
