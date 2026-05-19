import json
from datetime import datetime

import paho.mqtt.client as mqtt


BROKER_HOST = "localhost"
BROKER_PORT = 1883
TOPIC = "substation/breaker/control"
BREAKER_ID = "BRK-01"


OPTIONS = {
    "1": ("OPEN", "unauthorized"),
    "2": ("CLOSE", "unauthorized"),
    "3": ("OPEN", "operator"),
    "4": ("CLOSE", "operator"),
}


def print_menu() -> None:
    print()
    print("Breaker Control Command Publisher")
    print("1 - Unauthorized OPEN")
    print("2 - Unauthorized CLOSE")
    print("3 - Normal Operator OPEN")
    print("4 - Normal Operator CLOSE")
    print("q - Quit")


def build_payload(command: str, source: str) -> dict:
    return {
        "breakerId": BREAKER_ID,
        "command": command,
        "source": source,
        "timestamp": datetime.now().replace(microsecond=0).isoformat(),
    }


def publish_command(command: str, source: str) -> None:
    payload = build_payload(command, source)
    client = mqtt.Client(
        callback_api_version=mqtt.CallbackAPIVersion.VERSION2,
        client_id="breaker-attack-console",
    )

    print(f"Connecting to MQTT broker {BROKER_HOST}:{BROKER_PORT}...")
    client.connect(BROKER_HOST, BROKER_PORT, keepalive=60)
    client.loop_start()

    result = client.publish(TOPIC, json.dumps(payload), qos=0)
    result.wait_for_publish()

    client.loop_stop()
    client.disconnect()

    print(f"Published to {TOPIC}:")
    print(json.dumps(payload, indent=2))


def main() -> None:
    while True:
        print_menu()
        choice = input("Select command type: ").strip().lower()

        if choice in {"q", "quit", "exit"}:
            print("Exiting breaker command publisher.")
            break

        if choice not in OPTIONS:
            print("Invalid selection. Use 1, 2, 3, 4, or q.")
            continue

        command, source = OPTIONS[choice]
        try:
            publish_command(command, source)
        except Exception as exc:
            print(f"Publish failed: {exc}")


if __name__ == "__main__":
    main()
