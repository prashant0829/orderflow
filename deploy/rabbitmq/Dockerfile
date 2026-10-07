FROM rabbitmq:3.13-management-alpine

# Adds delayed messages: lets MassTransit schedule saga timeouts that survive restarts.
# The plugin file is committed next to this Dockerfile so the image builds without network access (see PLUGIN.md).
COPY rabbitmq_delayed_message_exchange-3.13.0.ez /opt/rabbitmq/plugins/
RUN chown rabbitmq:rabbitmq /opt/rabbitmq/plugins/rabbitmq_delayed_message_exchange-3.13.0.ez \
    && rabbitmq-plugins enable --offline rabbitmq_delayed_message_exchange
