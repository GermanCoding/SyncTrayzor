FROM php:8.4-apache@sha256:2c00551ed76646b92c929886e5521988a234f34bb495b387a4acbe5ce8597069
RUN a2enmod rewrite
RUN rm -f /var/www/html/index.html
COPY version-check.php /var/www/html/version-check.php
COPY .htaccess /var/www/html/.htaccess
RUN chown -R www-data:www-data /var/www/html/
EXPOSE 80