FROM php:8.5-apache@sha256:ceff12272a11843b83874fd37c41921a2f90fd492cad2d4b951fbcc640e62ebf
RUN a2enmod rewrite
RUN rm -f /var/www/html/index.html
COPY version-check.php /var/www/html/version-check.php
COPY .htaccess /var/www/html/.htaccess
RUN chown -R www-data:www-data /var/www/html/
EXPOSE 80