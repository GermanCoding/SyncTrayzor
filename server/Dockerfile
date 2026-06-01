FROM php:8.5-apache@sha256:613544da9bc080551901e6ee7784e6c573bcbc635453b96ce6f14be13d29b03d
RUN a2enmod rewrite
RUN rm -f /var/www/html/index.html
COPY version-check.php /var/www/html/version-check.php
COPY .htaccess /var/www/html/.htaccess
RUN chown -R www-data:www-data /var/www/html/
EXPOSE 80