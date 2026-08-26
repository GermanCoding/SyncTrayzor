FROM php:8.5-apache@sha256:705b53b7eb799eba54c2a0f1ab74993330388b8b9a2c5270f0dda78ecb3b29ca
RUN a2enmod rewrite
RUN rm -f /var/www/html/index.html
COPY version-check.php /var/www/html/version-check.php
COPY .htaccess /var/www/html/.htaccess
RUN chown -R www-data:www-data /var/www/html/
EXPOSE 80