# Bundled RabbitMQ plugin

`rabbitmq_delayed_message_exchange-3.13.0.ez` comes from the official release
https://github.com/rabbitmq/rabbitmq-delayed-message-exchange/releases/tag/v3.13.0 (licence: MPL 2.0).

SHA-256: `3479180df03fa830fd59ccf638a60eee8f0e8261524a8616685522d7b8d9a9ee`

It is committed here (45 KB) so that `docker build`, the integration tests and CI never depend on GitHub being reachable.
To upgrade, download the matching version for the RabbitMQ image in the Dockerfile and update this checksum.
